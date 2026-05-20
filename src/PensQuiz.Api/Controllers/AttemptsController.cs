using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PensQuiz.Api.Auth;
using PensQuiz.Api.Data;
using PensQuiz.Api.Models;

namespace PensQuiz.Api.Controllers;

[ApiController]
[Authorize]
public sealed class AttemptsController(IDbConnectionFactory db) : ControllerBase
{
    [HttpPost("api/quizzes/{quizId:guid}/attempts/start")]
    public async Task<ActionResult<StartAttemptResponse>> Start(Guid quizId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        var quiz = await connection.QuerySingleOrDefaultAsync<QuizDto>(
            new CommandDefinition(
                """
                select
                  id as Id,
                  author_id as AuthorId,
                  title as Title,
                  description as Description,
                  time_limit_minutes as TimeLimitMinutes,
                  major_id as MajorId,
                  course_id as CourseId,
                  lecturer_id as LecturerId,
                  folder_id as FolderId,
                  visibility as Visibility,
                  access as Access,
                  allow_copy as AllowCopy,
                  version_number as VersionNumber,
                  has_been_updated as HasBeenUpdated,
                  cover_image_url as CoverImageUrl,
                  created_at as CreatedAt,
                  updated_at as UpdatedAt
                from public.quizzes
                where id = @Id and deleted_at is null
                """,
                new { Id = quizId },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

        if (quiz is null)
        {
            return NotFound();
        }

        var canPlay =
            quiz.AuthorId == userId ||
            (quiz.Visibility == "published" && quiz.Access == "public");

        if (!canPlay)
        {
            return Forbid();
        }

        var snapshotId = await EnsureSnapshot(connection, transaction, quizId, quiz.VersionNumber, cancellationToken);

        var attemptId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                insert into public.attempts (id, user_id, quiz_id, snapshot_id, started_at)
                values (@Id, @UserId, @QuizId, @SnapshotId, @StartedAt)
                """,
                new
                {
                    Id = attemptId,
                    UserId = userId,
                    QuizId = quizId,
                    SnapshotId = snapshotId,
                    StartedAt = startedAt
                },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

        var questions = await LoadSnapshotQuestions(connection, transaction, snapshotId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Ok(new StartAttemptResponse(attemptId, snapshotId, startedAt, questions));
    }

    [HttpPost("api/attempts/{attemptId:guid}/answers")]
    public async Task<IActionResult> SaveAnswer(Guid attemptId, [FromBody] SaveAnswerRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        var attemptExists = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                select 1
                from public.attempts
                where id = @AttemptId and user_id = @UserId and submitted_at is null
                """,
                new { AttemptId = attemptId, UserId = userId },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

        if (attemptExists != 1)
        {
            return NotFound();
        }

        var attemptAnswerId = await connection.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                """
                insert into public.attempt_answers (id, attempt_id, snapshot_question_id)
                values (@Id, @AttemptId, @SnapshotQuestionId)
                on conflict (attempt_id, snapshot_question_id)
                do update set updated_at = now()
                returning id
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    AttemptId = attemptId,
                    SnapshotQuestionId = request.SnapshotQuestionId
                },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

        await connection.ExecuteAsync(
            new CommandDefinition(
                "delete from public.attempt_answer_options where attempt_answer_id = @AttemptAnswerId",
                new { AttemptAnswerId = attemptAnswerId },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

        if (request.SelectedSnapshotOptionIds.Length > 0)
        {
            foreach (var snapshotOptionId in request.SelectedSnapshotOptionIds.Distinct())
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        insert into public.attempt_answer_options (id, attempt_answer_id, snapshot_option_id)
                        values (@Id, @AttemptAnswerId, @SnapshotOptionId)
                        on conflict do nothing
                        """,
                        new
                        {
                            Id = Guid.NewGuid(),
                            AttemptAnswerId = attemptAnswerId,
                            SnapshotOptionId = snapshotOptionId
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken
                    )
                );
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("api/attempts/{attemptId:guid}/submit")]
    public async Task<ActionResult<SubmitAttemptResponse>> Submit(Guid attemptId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        var attempt = await connection.QuerySingleOrDefaultAsync<(Guid SnapshotId, DateTimeOffset? StartedAt)>(
            new CommandDefinition(
                """
                select snapshot_id as SnapshotId, started_at as StartedAt
                from public.attempts
                where id = @AttemptId and user_id = @UserId and submitted_at is null
                """,
                new { AttemptId = attemptId, UserId = userId },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

        if (attempt.SnapshotId == Guid.Empty)
        {
            return NotFound();
        }

        var snapshotId = attempt.SnapshotId;
        var questions = (await connection.QueryAsync<(Guid Id, string QuestionType)>(
            new CommandDefinition(
                """
                select id as Id, question_type as QuestionType
                from public.snapshot_questions
                where snapshot_id = @SnapshotId
                """,
                new { SnapshotId = snapshotId },
                transaction: transaction,
                cancellationToken: cancellationToken
            ))).ToList();

        var correctByQuestion = (await connection.QueryAsync<(Guid SnapshotQuestionId, Guid SnapshotOptionId)>(
            new CommandDefinition(
                """
                select snapshot_question_id as SnapshotQuestionId, id as SnapshotOptionId
                from public.snapshot_options
                where snapshot_question_id = any(@QuestionIds) and is_correct = true
                """,
                new { QuestionIds = questions.Select(q => q.Id).ToArray() },
                transaction: transaction,
                cancellationToken: cancellationToken
            ))).GroupBy(x => x.SnapshotQuestionId).ToDictionary(g => g.Key, g => g.Select(x => x.SnapshotOptionId).ToHashSet());

        var selectedByQuestion = (await connection.QueryAsync<(Guid SnapshotQuestionId, Guid SnapshotOptionId)>(
            new CommandDefinition(
                """
                select aa.snapshot_question_id as SnapshotQuestionId, aao.snapshot_option_id as SnapshotOptionId
                from public.attempt_answers aa
                join public.attempt_answer_options aao on aao.attempt_answer_id = aa.id
                where aa.attempt_id = @AttemptId
                """,
                new { AttemptId = attemptId },
                transaction: transaction,
                cancellationToken: cancellationToken
            ))).GroupBy(x => x.SnapshotQuestionId).ToDictionary(g => g.Key, g => g.Select(x => x.SnapshotOptionId).ToHashSet());

        var total = questions.Count;
        var correctCount = 0;

        foreach (var q in questions)
        {
            correctByQuestion.TryGetValue(q.Id, out var correctOptions);
            selectedByQuestion.TryGetValue(q.Id, out var selectedOptions);

            correctOptions ??= new HashSet<Guid>();
            selectedOptions ??= new HashSet<Guid>();

            var isCorrect = correctOptions.SetEquals(selectedOptions);
            if (isCorrect)
            {
                correctCount++;
            }
        }

        var score = total == 0 ? 0m : Math.Round((decimal)correctCount / total * 100m, 2);
        var submittedAt = DateTimeOffset.UtcNow;
        var durationSeconds = attempt.StartedAt is null ? null : (int?)Math.Max(0, (submittedAt - attempt.StartedAt.Value).TotalSeconds);

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                update public.attempts
                set
                  submitted_at = @SubmittedAt,
                  score = @Score,
                  duration_seconds = @DurationSeconds,
                  updated_at = now()
                where id = @AttemptId and user_id = @UserId
                """,
                new
                {
                    AttemptId = attemptId,
                    UserId = userId,
                    SubmittedAt = submittedAt,
                    Score = score,
                    DurationSeconds = durationSeconds
                },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

        await transaction.CommitAsync(cancellationToken);

        return Ok(new SubmitAttemptResponse(attemptId, score, submittedAt));
    }

    [HttpGet("api/attempts/{attemptId:guid}/review")]
    public async Task<ActionResult<AttemptReviewResponse>> GetReview(Guid attemptId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        // Verify attempt belongs to the user and is submitted
        var attempt = await connection.QuerySingleOrDefaultAsync<(Guid SnapshotId, decimal? Score)>(
            new CommandDefinition(
                """
                select snapshot_id as SnapshotId, score as Score
                from public.attempts
                where id = @AttemptId and user_id = @UserId and submitted_at is not null
                """,
                new { AttemptId = attemptId, UserId = userId },
                cancellationToken: cancellationToken
            )
        );

        if (attempt.SnapshotId == Guid.Empty)
        {
            return NotFound();
        }

        // Get snapshot questions
        var questions = (await connection.QueryAsync<(Guid Id, string Content, string QuestionType, int OrderIndex, string? Explanation)>(
            new CommandDefinition(
                """
                select
                  id as Id,
                  content as Content,
                  question_type as QuestionType,
                  order_index as OrderIndex,
                  explanation as Explanation
                from public.snapshot_questions
                where snapshot_id = @SnapshotId
                order by order_index asc
                """,
                new { SnapshotId = attempt.SnapshotId },
                cancellationToken: cancellationToken
            ))).ToList();

        // Get snapshot options with is_correct!
        var options = (await connection.QueryAsync<(Guid Id, Guid SnapshotQuestionId, string Content, bool IsCorrect, int OrderIndex)>(
            new CommandDefinition(
                """
                select
                  id as Id,
                  snapshot_question_id as SnapshotQuestionId,
                  content as Content,
                  is_correct as IsCorrect,
                  order_index as OrderIndex
                from public.snapshot_options
                where snapshot_question_id = any(@QuestionIds)
                order by order_index asc
                """,
                new { QuestionIds = questions.Select(q => q.Id).ToArray() },
                cancellationToken: cancellationToken
            ))).ToList();

        // Get user's selected option IDs for this attempt
        var selectedByQuestion = (await connection.QueryAsync<(Guid SnapshotQuestionId, Guid SnapshotOptionId)>(
            new CommandDefinition(
                """
                select aa.snapshot_question_id as SnapshotQuestionId, aao.snapshot_option_id as SnapshotOptionId
                from public.attempt_answers aa
                join public.attempt_answer_options aao on aao.attempt_answer_id = aa.id
                where aa.attempt_id = @AttemptId
                """,
                new { AttemptId = attemptId },
                cancellationToken: cancellationToken
            ))).GroupBy(x => x.SnapshotQuestionId).ToDictionary(g => g.Key, g => g.Select(x => x.SnapshotOptionId).ToList());

        var reviewQuestions = questions.Select(q => {
            selectedByQuestion.TryGetValue(q.Id, out var selected);
            selected ??= [];

            var qOptions = options
                .Where(o => o.SnapshotQuestionId == q.Id)
                .Select(o => new SnapshotOptionReviewDto(o.Id, o.Content, o.IsCorrect, o.OrderIndex))
                .ToList();

            return new SnapshotQuestionReviewDto(
                q.Id,
                q.Content,
                q.QuestionType,
                q.OrderIndex,
                q.Explanation,
                qOptions,
                selected
            );
        }).ToList();

        return Ok(new AttemptReviewResponse(attemptId, attempt.Score ?? 0m, reviewQuestions));
    }

    private static async Task<Guid> EnsureSnapshot(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid quizId,
        int versionNumber,
        CancellationToken cancellationToken)
    {
        var existing = await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(
                """
                select id
                from public.quiz_snapshots
                where quiz_id = @QuizId and version_number = @VersionNumber
                """,
                new { QuizId = quizId, VersionNumber = versionNumber },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

        if (existing is not null && existing.Value != Guid.Empty)
        {
            return existing.Value;
        }

        var snapshotId = Guid.NewGuid();
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                insert into public.quiz_snapshots (id, quiz_id, version_number)
                values (@Id, @QuizId, @VersionNumber)
                on conflict (quiz_id, version_number) do nothing
                """,
                new { Id = snapshotId, QuizId = quizId, VersionNumber = versionNumber },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

        var finalSnapshotId = await connection.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                """
                select id
                from public.quiz_snapshots
                where quiz_id = @QuizId and version_number = @VersionNumber
                """,
                new { QuizId = quizId, VersionNumber = versionNumber },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

        if (finalSnapshotId != snapshotId)
        {
            return finalSnapshotId;
        }

        var questions = (await connection.QueryAsync<QuizQuestionDto>(
            new CommandDefinition(
                """
                select
                  id as Id,
                  quiz_id as QuizId,
                  content as Content,
                  question_type as QuestionType,
                  order_index as OrderIndex,
                  image_url as ImageUrl
                from public.questions
                where quiz_id = @QuizId and deleted_at is null
                order by order_index asc
                """,
                new { QuizId = quizId },
                transaction: transaction,
                cancellationToken: cancellationToken
            ))).ToList();

        var questionIdMap = new Dictionary<Guid, Guid>();
        foreach (var q in questions)
        {
            var snapshotQuestionId = Guid.NewGuid();
            questionIdMap[q.Id] = snapshotQuestionId;
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    insert into public.snapshot_questions (
                      id,
                      snapshot_id,
                      original_question_id,
                      content,
                      question_type,
                      order_index,
                      explanation
                    )
                    values (
                      @Id,
                      @SnapshotId,
                      @OriginalQuestionId,
                      @Content,
                      @QuestionType,
                      @OrderIndex,
                      null
                    )
                    """,
                    new
                    {
                        Id = snapshotQuestionId,
                        SnapshotId = snapshotId,
                        OriginalQuestionId = q.Id,
                        q.Content,
                        q.QuestionType,
                        q.OrderIndex
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                )
            );
        }

        if (questions.Count == 0)
        {
            return snapshotId;
        }

        var options = (await connection.QueryAsync<QuizOptionDto>(
            new CommandDefinition(
                """
                select
                  id as Id,
                  question_id as QuestionId,
                  content as Content,
                  is_correct as IsCorrect,
                  order_index as OrderIndex
                from public.options
                where question_id = any(@QuestionIds) and deleted_at is null
                order by order_index asc
                """,
                new { QuestionIds = questions.Select(q => q.Id).ToArray() },
                transaction: transaction,
                cancellationToken: cancellationToken
            ))).ToList();

        foreach (var o in options)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    insert into public.snapshot_options (
                      id,
                      snapshot_question_id,
                      original_option_id,
                      content,
                      is_correct,
                      order_index
                    )
                    values (
                      @Id,
                      @SnapshotQuestionId,
                      @OriginalOptionId,
                      @Content,
                      @IsCorrect,
                      @OrderIndex
                    )
                    """,
                    new
                    {
                        Id = Guid.NewGuid(),
                        SnapshotQuestionId = questionIdMap[o.QuestionId],
                        OriginalOptionId = o.Id,
                        o.Content,
                        o.IsCorrect,
                        o.OrderIndex
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                )
            );
        }

        return snapshotId;
    }

    private static async Task<IReadOnlyList<SnapshotQuestionDto>> LoadSnapshotQuestions(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid snapshotId,
        CancellationToken cancellationToken)
    {
        var questions = (await connection.QueryAsync<(Guid Id, string Content, string QuestionType, int OrderIndex, string? Explanation)>(
            new CommandDefinition(
                """
                select
                  id as Id,
                  content as Content,
                  question_type as QuestionType,
                  order_index as OrderIndex,
                  explanation as Explanation
                from public.snapshot_questions
                where snapshot_id = @SnapshotId
                order by order_index asc
                """,
                new { SnapshotId = snapshotId },
                transaction: transaction,
                cancellationToken: cancellationToken
            ))).ToList();

        if (questions.Count == 0)
        {
            return [];
        }

        var options = (await connection.QueryAsync<(Guid Id, Guid SnapshotQuestionId, string Content, int OrderIndex, bool IsCorrect)>(
            new CommandDefinition(
                """
                select
                  id as Id,
                  snapshot_question_id as SnapshotQuestionId,
                  content as Content,
                  order_index as OrderIndex,
                  is_correct as IsCorrect
                from public.snapshot_options
                where snapshot_question_id = any(@QuestionIds)
                order by order_index asc
                """,
                new { QuestionIds = questions.Select(q => q.Id).ToArray() },
                transaction: transaction,
                cancellationToken: cancellationToken
            ))).ToList();

        var optionsByQuestion = options
            .GroupBy(o => o.SnapshotQuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return questions
            .Select(q => {
                var qOptions = optionsByQuestion.TryGetValue(q.Id, out var list) ? list : [];
                var correctCount = qOptions.Count(o => o.IsCorrect);
                var dtos = qOptions.Select(o => new SnapshotOptionDto(o.Id, o.Content, o.OrderIndex)).ToList();
                return new SnapshotQuestionDto(
                    q.Id,
                    q.Content,
                    q.QuestionType,
                    q.OrderIndex,
                    q.Explanation,
                    correctCount,
                    dtos
                );
            })
            .ToList();
    }
}

