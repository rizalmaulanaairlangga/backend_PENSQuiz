using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PensQuiz.Api.Auth;
using PensQuiz.Api.Services;

using Dapper;
using PensQuiz.Api.Data;

namespace PensQuiz.Api.Controllers;

[ApiController]
[Route("api/upload")]
[Authorize]
public sealed class UploadController(IStorageService storageService, IDbConnectionFactory db) : ControllerBase
{
    private static readonly string[] AllowedTypes = ["image/jpeg", "image/png", "image/webp", "image/jpg", "image/avif"];

    [HttpPost("quiz-cover")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadQuizCover(
        [FromForm] Guid quizId,
        [FromForm] string visibility,
        IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { error = "No file provided" });
        if (!AllowedTypes.Contains(file.ContentType.ToLower())) return BadRequest(new { error = "Invalid file type" });
        if (file.Length > 10 * 1024 * 1024) return BadRequest(new { error = "File too large (max 10MB)" });

        var userId = User.GetUserId();
        await using var connection = db.CreateConnection();
        var quizInfo = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "select author_id from public.quizzes where id = @QuizId and deleted_at is null",
            new { QuizId = quizId }
        );
        if (quizInfo != null && (Guid)quizInfo.author_id != userId) return Forbid();

        bool isPublic = visibility.Equals("public", StringComparison.OrdinalIgnoreCase);
        await using var stream = file.OpenReadStream();
        
        var url = await storageService.UploadQuizCover(quizId, isPublic, file.FileName, file.ContentType, stream);
        if (url == null) return StatusCode(500, new { error = "Upload failed" });

        return Ok(new { url });
    }

    [HttpPost("question-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadQuestionImage(
        [FromForm] Guid questionId,
        [FromForm] string visibility,
        IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { error = "No file provided" });
        if (!AllowedTypes.Contains(file.ContentType.ToLower())) return BadRequest(new { error = "Invalid file type" });
        if (file.Length > 10 * 1024 * 1024) return BadRequest(new { error = "File too large (max 10MB)" });

        var userId = User.GetUserId();
        await using var connection = db.CreateConnection();
        var questionInfo = await connection.QuerySingleOrDefaultAsync<dynamic>(
            @"select qz.author_id 
              from public.questions q
              join public.quizzes qz on q.quiz_id = qz.id
              where q.id = @QuestionId and qz.deleted_at is null",
            new { QuestionId = questionId }
        );
        if (questionInfo != null && (Guid)questionInfo.author_id != userId) return Forbid();

        bool isPublic = visibility.Equals("public", StringComparison.OrdinalIgnoreCase);
        await using var stream = file.OpenReadStream();

        var url = await storageService.UploadQuestionImage(questionId, isPublic, file.FileName, file.ContentType, stream);
        if (url == null) return StatusCode(500, new { error = "Upload failed" });

        return Ok(new { url });
    }

    [HttpPost("profile-avatar")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadProfileAvatar(IFormFile file)
    {
        var userId = User.GetUserId();
        if (file == null || file.Length == 0) return BadRequest(new { error = "No file provided" });
        if (!AllowedTypes.Contains(file.ContentType.ToLower())) return BadRequest(new { error = "Invalid file type" });
        if (file.Length > 2 * 1024 * 1024) return BadRequest(new { error = "File too large (max 2MB)" });

        await using var stream = file.OpenReadStream();

        var url = await storageService.UploadProfileImage(userId, file.FileName, file.ContentType, stream);
        if (url == null) return StatusCode(500, new { error = "Upload failed" });

        return Ok(new { url });
    }
}
