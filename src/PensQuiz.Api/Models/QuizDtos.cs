namespace PensQuiz.Api.Models;

public sealed class QuizDto
{
    public Guid Id { get; init; }
    public string? Slug { get; set; }
    public Guid AuthorId { get; init; }
    public string? AuthorName { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public int? TimeLimitMinutes { get; init; }
    public Guid? MajorId { get; init; }
    public string? MajorName { get; init; }
    public Guid? CourseId { get; init; }
    public string? CourseName { get; init; }
    public Guid? LecturerId { get; init; }
    public string? LecturerName { get; init; }
    public Guid? FolderId { get; init; }
    public string Visibility { get; init; } = "draft";
    public string Access { get; init; } = "private";
    public bool AllowCopy { get; init; }
    public int VersionNumber { get; init; }
    public bool HasBeenUpdated { get; init; }
    public string? CoverImageUrl { get; init; }
    public int QuestionCount { get; init; }
    public List<string>? Tags { get; set; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record HomeQuizzesResponse(
    List<QuizDto> History,
    List<QuizDto> Recommended,
    List<QuizDto> PopularInMajor,
    List<QuizDto> Trending
);

public sealed record QuizDetailResponse(
    QuizDto Quiz,
    List<QuizDto> RelatedQuizzes
);

public sealed record SaveQuizOptionRequest(
    Guid? Id,
    string Content,
    bool IsCorrect,
    int OrderIndex
);

public sealed record SaveQuizQuestionRequest(
    Guid? Id,
    string Content,
    string QuestionType,
    int OrderIndex,
    string? ImageUrl,
    List<SaveQuizOptionRequest> Options
);

public sealed record CreateQuizRequest(
    string Title,
    string? Description,
    int? TimeLimitMinutes,
    Guid? MajorId,
    Guid? CourseId,
    Guid? LecturerId,
    Guid? FolderId,
    string Visibility = "draft",
    string Access = "private",
    bool AllowCopy = false,
    string? CoverImageUrl = null,
    List<SaveQuizQuestionRequest>? Questions = null,
    List<string>? Tags = null
);

public sealed record UpdateQuizRequest(
    string Title,
    string? Description,
    int? TimeLimitMinutes,
    Guid? MajorId,
    Guid? CourseId,
    Guid? LecturerId,
    Guid? FolderId,
    string Visibility,
    string Access,
    bool AllowCopy,
    string? CoverImageUrl,
    List<SaveQuizQuestionRequest>? Questions = null,
    List<string>? Tags = null
);

public sealed record MoveQuizRequest(Guid? FolderId);

public sealed record QuizQuestionDto(
    Guid Id,
    Guid QuizId,
    string Content,
    string QuestionType,
    int OrderIndex,
    string? ImageUrl = null
);

public sealed record QuizOptionDto(
    Guid Id,
    Guid QuestionId,
    string Content,
    bool IsCorrect,
    int OrderIndex
);

public sealed record QuizFullQuestionDto(
    Guid Id,
    Guid QuizId,
    string Content,
    string QuestionType,
    int OrderIndex,
    string? ImageUrl,
    List<QuizOptionDto> Options
);

public sealed record QuizFullDto(
    QuizDto Quiz,
    List<QuizFullQuestionDto> Questions
);


