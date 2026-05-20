namespace PensQuiz.Api.Models;

public sealed record SnapshotQuestionDto(
    Guid Id,
    string Content,
    string QuestionType,
    int OrderIndex,
    string? Explanation,
    int CorrectOptionCount,
    IReadOnlyList<SnapshotOptionDto> Options
);

public sealed record SnapshotOptionDto(Guid Id, string Content, int OrderIndex);

public sealed record StartAttemptResponse(
    Guid AttemptId,
    Guid SnapshotId,
    DateTimeOffset StartedAt,
    IReadOnlyList<SnapshotQuestionDto> Questions
);

public sealed record SaveAnswerRequest(Guid SnapshotQuestionId, Guid[] SelectedSnapshotOptionIds);

public sealed record SubmitAttemptResponse(Guid AttemptId, decimal Score, DateTimeOffset SubmittedAt);

public sealed record SnapshotOptionReviewDto(Guid Id, string Content, bool IsCorrect, int OrderIndex);

public sealed record SnapshotQuestionReviewDto(
    Guid Id,
    string Content,
    string QuestionType,
    int OrderIndex,
    string? Explanation,
    IReadOnlyList<SnapshotOptionReviewDto> Options,
    IReadOnlyList<Guid> SelectedOptions
);

public sealed record AttemptReviewResponse(
    Guid AttemptId,
    decimal Score,
    IReadOnlyList<SnapshotQuestionReviewDto> Questions
);

