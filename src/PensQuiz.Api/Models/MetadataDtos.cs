using System.Text.Json.Serialization;

namespace PensQuiz.Api.Models;

public sealed record MajorDto(Guid Id, string Name);
public sealed record CourseDto(Guid Id, string Name, Guid? MajorId);
public sealed record FolderDto(
    Guid Id, 
    string Name, 
    [property: JsonPropertyName("item_count")] long ItemCount = 0, 
    [property: JsonPropertyName("updated_at")] DateTime? UpdatedAt = null);
public sealed record TagDto(
    Guid Id, 
    string Name, 
    [property: JsonPropertyName("usage_count")] int UsageCount = 0);

public sealed record FolderQuizDto(
    Guid Id,
    string? Slug,
    string Title,
    string? Description,
    string Visibility,
    string? CoverImageUrl,
    string? MajorName,
    string? CourseName,
    DateTime? UpdatedAt
);

public sealed record FolderDetailResponse(FolderDto Folder, List<FolderQuizDto> Quizzes);
