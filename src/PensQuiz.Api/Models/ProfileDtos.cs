namespace PensQuiz.Api.Models;

public sealed class ProfileDto
{
    public Guid Id { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Username { get; init; }
    public Guid? MajorId { get; init; }
    public string? MajorName { get; init; }
    public int? YearOfEntry { get; init; }
    public string Role { get; init; } = "student";
    public string? AvatarUrl { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record UpdateAvatarRequest(string? AvatarUrl);

public sealed record UpdateProfileRequest(
    string? FirstName,
    string? LastName,
    string? Username,
    Guid? MajorId,
    int? YearOfEntry
);
