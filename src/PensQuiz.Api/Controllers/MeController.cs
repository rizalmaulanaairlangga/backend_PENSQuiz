using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PensQuiz.Api.Auth;
using PensQuiz.Api.Data;
using PensQuiz.Api.Models;

using PensQuiz.Api.Services;

namespace PensQuiz.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController(IDbConnectionFactory db, IStorageService storageService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await using var connection = db.CreateConnection();
        var profile = await connection.QuerySingleOrDefaultAsync<ProfileDto>(
            new CommandDefinition(
                """
                select
                  p.id as Id,
                  p.first_name as FirstName,
                  p.last_name as LastName,
                  p.username as Username,
                  p.major_id as MajorId,
                  m.name as MajorName,
                  p.year_of_entry as YearOfEntry,
                  p.role as Role,
                  p.avatar_url as AvatarUrl,
                  p.created_at as CreatedAt,
                  p.updated_at as UpdatedAt
                from public.profiles p
                left join public.majors m on p.major_id = m.id
                where p.id = @UserId and p.deleted_at is null
                """,
                new { UserId = userId },
                cancellationToken: cancellationToken
            )
        );

        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPatch]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await using var connection = db.CreateConnection();
        var updated = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                update public.profiles
                set
                  first_name = @FirstName,
                  last_name = @LastName,
                  username = @Username,
                  major_id = @MajorId,
                  year_of_entry = @YearOfEntry,
                  updated_at = now()
                where id = @UserId and deleted_at is null
                """,
                new
                {
                    UserId = userId,
                    request.FirstName,
                    request.LastName,
                    request.Username,
                    request.MajorId,
                    request.YearOfEntry
                },
                cancellationToken: cancellationToken
            )
        );

        return updated == 0 ? NotFound() : NoContent();
    }

    [HttpPut("avatar")]
    public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await using var connection = db.CreateConnection();

        var oldAvatarUrl = await connection.QuerySingleOrDefaultAsync<string>(
            "select avatar_url from public.profiles where id = @UserId and deleted_at is null",
            new { UserId = userId }
        );

        var updated = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                update public.profiles
                set
                  avatar_url = @AvatarUrl,
                  updated_at = now()
                where id = @UserId and deleted_at is null
                """,
                new
                {
                    UserId = userId,
                    request.AvatarUrl
                },
                cancellationToken: cancellationToken
            )
        );

        if (updated > 0 && oldAvatarUrl != null && oldAvatarUrl != request.AvatarUrl)
        {
            var storageInfo = ParseStorageUrl(oldAvatarUrl);
            if (storageInfo.HasValue)
            {
                await storageService.DeleteImage(storageInfo.Value.bucket, storageInfo.Value.path);
            }
        }

        return updated == 0 ? NotFound() : NoContent();
    }

    private static (string bucket, string path)? ParseStorageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!url.Contains("/storage/v1/object/public/"))
        {
            if (url.StartsWith("profile-images/")) return ("profile-images", url);
            return null;
        }

        var parts = url.Split("/storage/v1/object/public/");
        if (parts.Length < 2) return null;

        var bucketAndPath = parts[1];
        var firstSlash = bucketAndPath.IndexOf('/');
        if (firstSlash == -1) return null;

        var bucket = bucketAndPath[..firstSlash];
        var path = bucketAndPath[(firstSlash + 1)..];

        return (bucket, path);
    }

}

