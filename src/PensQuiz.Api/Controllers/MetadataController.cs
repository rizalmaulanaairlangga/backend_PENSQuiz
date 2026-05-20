using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PensQuiz.Api.Auth;
using PensQuiz.Api.Data;
using PensQuiz.Api.Models;

namespace PensQuiz.Api.Controllers;

[ApiController]
[Route("api/metadata")]
[Authorize]
public sealed class MetadataController(IDbConnectionFactory db) : ControllerBase
{
    [HttpGet("majors")]
    public async Task<IActionResult> GetMajors(CancellationToken cancellationToken)
    {
        await using var connection = db.CreateConnection();
        var majors = await connection.QueryAsync<MajorDto>(
            new CommandDefinition("select id as Id, name as Name from public.majors order by name", cancellationToken: cancellationToken)
        );
        return Ok(majors);
    }

    [HttpGet("courses")]
    public async Task<IActionResult> GetCourses([FromQuery] Guid? majorId, CancellationToken cancellationToken)
    {
        await using var connection = db.CreateConnection();
        var courses = await connection.QueryAsync<CourseDto>(
            new CommandDefinition(
                "select id as Id, name as Name, major_id as MajorId from public.courses where @MajorId is null or major_id = @MajorId order by name",
                new { MajorId = majorId },
                cancellationToken: cancellationToken
            )
        );
        return Ok(courses);
    }

    [HttpGet("folders")]
    public async Task<IActionResult> GetFolders(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var connection = db.CreateConnection();
        var folders = await connection.QueryAsync<FolderDto>(
            new CommandDefinition(
                """
                select
                  f.id as Id,
                  f.name as Name,
                  (select count(*) from public.quizzes q where q.folder_id = f.id and q.deleted_at is null) as ItemCount,
                  f.updated_at as UpdatedAt
                from public.folders f
                where f.user_id = @UserId
                order by f.name
                """,
                new { UserId = userId },
                cancellationToken: cancellationToken
            )
        );
        return Ok(folders);
    }

    [HttpGet("folders/{id:guid}")]
    public async Task<IActionResult> GetFolder(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var connection = db.CreateConnection();

        var folder = await connection.QuerySingleOrDefaultAsync<FolderDto>(
            new CommandDefinition(
                """
                select
                  f.id as Id,
                  f.name as Name,
                  (select count(*) from public.quizzes q where q.folder_id = f.id and q.deleted_at is null) as ItemCount,
                  f.updated_at as UpdatedAt
                from public.folders f
                where f.id = @Id and f.user_id = @UserId
                """,
                new { Id = id, UserId = userId },
                cancellationToken: cancellationToken
            )
        );

        if (folder is null) return NotFound();

        var quizzes = await connection.QueryAsync<FolderQuizDto>(
            new CommandDefinition(
                """
                select
                  q.id as Id,
                  q.slug as Slug,
                  q.title as Title,
                  q.description as Description,
                  q.visibility as Visibility,
                  q.cover_image_url as CoverImageUrl,
                  m.name as MajorName,
                  c.name as CourseName,
                  q.updated_at as UpdatedAt
                from public.quizzes q
                left join public.majors m on q.major_id = m.id
                left join public.courses c on q.course_id = c.id
                where q.folder_id = @FolderId and q.deleted_at is null
                order by q.updated_at desc
                """,
                new { FolderId = id },
                cancellationToken: cancellationToken
            )
        );

        return Ok(new FolderDetailResponse(folder, quizzes.ToList()));
    }

    public sealed record RenameFolderRequest(string Name);
    [HttpPatch("folders/{id:guid}")]
    public async Task<IActionResult> RenameFolder(Guid id, [FromBody] RenameFolderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required" });
        var userId = User.GetUserId();
        await using var connection = db.CreateConnection();
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "update public.folders set name = @Name, updated_at = now() where id = @Id and user_id = @UserId",
                new { Id = id, request.Name, UserId = userId },
                cancellationToken: cancellationToken
            )
        );
        if (affected == 0) return NotFound();
        return NoContent();
    }

    [HttpDelete("folders/{id:guid}")]
    public async Task<IActionResult> DeleteFolder(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var connection = db.CreateConnection();
        // Unlink quizzes before deleting
        await connection.ExecuteAsync(
            new CommandDefinition(
                "update public.quizzes set folder_id = null, updated_at = now() where folder_id = @FolderId and deleted_at is null",
                new { FolderId = id },
                cancellationToken: cancellationToken
            )
        );
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "delete from public.folders where id = @Id and user_id = @UserId",
                new { Id = id, UserId = userId },
                cancellationToken: cancellationToken
            )
        );
        if (affected == 0) return NotFound();
        return NoContent();
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetTags(CancellationToken cancellationToken)
    {
        await using var connection = db.CreateConnection();
        var tags = await connection.QueryAsync<TagDto>(
            new CommandDefinition("select id as Id, name as Name, usage_count as UsageCount from public.tags order by usage_count desc, name", cancellationToken: cancellationToken)
        );
        return Ok(tags);
    }

    public sealed record CreateTagRequest(string Name);
    [HttpPost("tags")]
    public async Task<IActionResult> CreateTag([FromBody] CreateTagRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required" });
        var id = Guid.NewGuid();
        await using var connection = db.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition("insert into public.tags (id, name, created_at, updated_at) values (@Id, @Name, now(), now())", new { Id = id, request.Name }, cancellationToken: cancellationToken)
        );
        return Ok(new { id, name = request.Name });
    }

    [HttpPost("tags/{id:guid}/increment")]
    public async Task<IActionResult> IncrementTagUsage(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = db.CreateConnection();
        var affected = await connection.ExecuteAsync(
            new CommandDefinition("update public.tags set usage_count = usage_count + 1, updated_at = now() where id = @Id", new { Id = id }, cancellationToken: cancellationToken)
        );
        if (affected == 0) return NotFound();
        return NoContent();
    }

    public sealed record CreateMajorRequest(string Name);
    [HttpPost("majors")]
    public async Task<IActionResult> CreateMajor([FromBody] CreateMajorRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required" });
        var id = Guid.NewGuid();
        var code = request.Name.Replace(" ", "-").ToUpper() + "-" + Guid.NewGuid().ToString()[..4];
        await using var connection = db.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition("insert into public.majors (id, name, code, created_at, updated_at) values (@Id, @Name, @Code, now(), now())", new { Id = id, request.Name, Code = code }, cancellationToken: cancellationToken)
        );
        return Ok(new { id, name = request.Name });
    }

    public sealed record CreateCourseRequest(string Name, Guid? MajorId);
    [HttpPost("courses")]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required" });
        var id = Guid.NewGuid();
        var code = request.Name.Replace(" ", "-").ToUpper() + "-" + Guid.NewGuid().ToString()[..4];
        await using var connection = db.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition("insert into public.courses (id, name, major_id, code, created_at, updated_at) values (@Id, @Name, @MajorId, @Code, now(), now())", new { Id = id, request.Name, request.MajorId, Code = code }, cancellationToken: cancellationToken)
        );


        return Ok(new { id, name = request.Name, majorId = request.MajorId });
    }

    public sealed record CreateFolderRequest(string Name);
    [HttpPost("folders")]
    public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "Name is required" });
        var userId = User.GetUserId();
        var id = Guid.NewGuid();
        await using var connection = db.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition("insert into public.folders (id, name, user_id, created_at, updated_at) values (@Id, @Name, @UserId, now(), now())", new { Id = id, request.Name, UserId = userId }, cancellationToken: cancellationToken)
        );
        return Ok(new { id, name = request.Name });
    }
}
