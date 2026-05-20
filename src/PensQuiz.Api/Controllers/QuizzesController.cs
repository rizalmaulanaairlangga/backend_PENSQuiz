using System.Data;
using System.Linq;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PensQuiz.Api.Auth;
using PensQuiz.Api.Data;
using PensQuiz.Api.Models;
using PensQuiz.Api.Services;

namespace PensQuiz.Api.Controllers;

[ApiController]
[Route("api/quizzes")]
[Authorize]
public sealed class QuizzesController(
    IDbConnectionFactory db, 
    ILogger<QuizzesController> logger,
    IStorageService storageService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuizDto>>> Discover(
        [FromQuery] string? visibility,
        [FromQuery] string? access,
        [FromQuery] Guid? majorId,
        [FromQuery] Guid? courseId,
        [FromQuery] string? search,
        [FromQuery] string[]? tags,
        CancellationToken cancellationToken)
    {
        await using var connection = db.CreateConnection();

        var query = """
            select
              q.id as Id,
              q.slug as Slug,
              q.author_id as AuthorId,
              coalesce(nullif(concat_ws(' ', ap.first_name, ap.last_name), ''), ap.username, q.author_id::text) as AuthorName,
              q.title as Title,
              q.description as Description,
              q.time_limit_minutes as TimeLimitMinutes,
              q.major_id as MajorId,
              m.name as MajorName,
              q.course_id as CourseId,
              c.name as CourseName,
              q.lecturer_id as LecturerId,
              l.full_name as LecturerName,
              q.folder_id as FolderId,
              q.visibility as Visibility,
              q.access as Access,
              q.allow_copy as AllowCopy,
              q.version_number as VersionNumber,
              q.has_been_updated as HasBeenUpdated,
              q.cover_image_url as CoverImageUrl,
              q.created_at as CreatedAt,
              q.updated_at as UpdatedAt,
              (select count(*) from public.questions where quiz_id = q.id and deleted_at is null) as QuestionCount
            from public.quizzes q
            left join public.profiles ap on q.author_id = ap.id
            left join public.lecturers l on q.lecturer_id = l.id
            left join public.majors m on q.major_id = m.id
            left join public.courses c on q.course_id = c.id
            where q.deleted_at is null
              and (@Visibility is null or q.visibility = @Visibility)
              and (@Access is null or q.access = @Access)
              and (@MajorId is null or q.major_id = @MajorId)
              and (@CourseId is null or q.course_id = @CourseId)
              and (@Search is null or (
                  q.title ilike @Search 
                  or q.description ilike @Search
                  or m.name ilike @Search
                  or c.name ilike @Search
                  or concat_ws(' ', ap.first_name, ap.last_name) ilike @Search
                  or ap.username ilike @Search
                  or l.full_name ilike @Search
                  or exists (
                      select 1 from public.quiz_tags qt 
                      join public.tags t on qt.tag_id = t.id 
                      where qt.quiz_id = q.id and t.name ilike @Search
                  )
              ))
            """;

        if (tags != null && tags.Length > 0)
        {
            query += " and exists (select 1 from public.quiz_tags qt join public.tags t on qt.tag_id = t.id where qt.quiz_id = q.id and t.name = any(@Tags))";
        }

        query += " order by q.updated_at desc limit 100";

        var quizzes = await connection.QueryAsync<QuizDto>(
            new CommandDefinition(
                query,
                new
                {
                    Visibility = visibility,
                    Access = access,
                    MajorId = majorId,
                    CourseId = courseId,
                    Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%",
                    Tags = tags
                },
                cancellationToken: cancellationToken
            )
        );

        var result = quizzes.ToList();
        await PopulateTags(connection, result, cancellationToken);
        return Ok(result);
    }

    [HttpGet("home")]
    public async Task<ActionResult<HomeQuizzesResponse>> Home(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var connection = db.CreateConnection();

        // Get user's major for "Popular In Your Major"
        var userMajorId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            "select major_id from public.profiles where id = @UserId",
            new { UserId = userId }
        );

        const string baseQuery = """
            select
              q.id as Id, q.slug as Slug, q.author_id as AuthorId,
              coalesce(nullif(concat_ws(' ', ap.first_name, ap.last_name), ''), ap.username, q.author_id::text) as AuthorName,
              q.title as Title, q.description as Description, q.time_limit_minutes as TimeLimitMinutes,
              q.major_id as MajorId, m.name as MajorName,
              q.course_id as CourseId, c.name as CourseName,
              q.lecturer_id as LecturerId, l.full_name as LecturerName,
              q.folder_id as FolderId, q.visibility as Visibility, q.access as Access,
              q.allow_copy as AllowCopy, q.version_number as VersionNumber, q.has_been_updated as HasBeenUpdated,
              q.cover_image_url as CoverImageUrl,
              q.created_at as CreatedAt, q.updated_at as UpdatedAt,
              (select count(*) from public.questions where quiz_id = q.id and deleted_at is null) as QuestionCount
            from public.quizzes q
            left join public.profiles ap on q.author_id = ap.id
            left join public.lecturers l on q.lecturer_id = l.id
            left join public.majors m on q.major_id = m.id
            left join public.courses c on q.course_id = c.id
            """;

        // 1. History (Quizzes opened by user)
        var history = (await connection.QueryAsync<QuizDto>(
            $"{baseQuery} inner join public.quiz_histories qh on q.id = qh.quiz_id where qh.user_id = @UserId and q.deleted_at is null order by qh.last_opened_at desc limit 10",
            new { UserId = userId }
        )).ToList();

        // 2. Recommended (Latest public quizzes)
        var recommended = (await connection.QueryAsync<QuizDto>(
            $"{baseQuery} where q.visibility = 'published' and q.access = 'public' and q.deleted_at is null order by q.created_at desc limit 10"
        )).ToList();

        // 3. Popular in Major
        List<QuizDto> popularInMajor = [];
        if (userMajorId.HasValue)
        {
            popularInMajor = (await connection.QueryAsync<QuizDto>(
                $"{baseQuery} where q.major_id = @MajorId and q.visibility = 'published' and q.access = 'public' and q.deleted_at is null order by q.created_at desc limit 10",
                new { MajorId = userMajorId }
            )).ToList();
        }

        // 4. Trending (Most attempts in last 7 days)
        var trending = (await connection.QueryAsync<QuizDto>(
            $"{baseQuery} inner join (select quiz_id, count(*) as attempt_count from public.attempts where started_at > now() - interval '7 days' group by quiz_id) trending on q.id = trending.quiz_id where q.visibility = 'published' and q.access = 'public' and q.deleted_at is null order by trending.attempt_count desc limit 10"
        )).ToList();

        var allQuizzes = history.Concat(recommended).Concat(popularInMajor).Concat(trending).ToList();
        await PopulateTags(connection, allQuizzes, cancellationToken);

        return Ok(new HomeQuizzesResponse(history, recommended, popularInMajor, trending));
    }

    private record TagQueryResult(Guid QuizId, string Name);

    private async Task PopulateTags(IDbConnection connection, List<QuizDto> quizzes, CancellationToken cancellationToken)
    {
        if (quizzes.Count == 0) return;
        var quizIds = quizzes.Select(q => q.Id).ToList();

        var tags = await connection.QueryAsync<TagQueryResult>(
            new CommandDefinition(
                "select qt.quiz_id as QuizId, t.name as Name from public.quiz_tags qt join public.tags t on qt.tag_id = t.id where qt.quiz_id = any(@QuizIds)",
                new { QuizIds = quizIds },
                cancellationToken: cancellationToken
            )
        );

        var tagMap = tags.GroupBy(x => x.QuizId).ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToList());
        foreach (var quiz in quizzes)
        {
            if (tagMap.TryGetValue(quiz.Id, out var quizTags))
            {
                quiz.Tags = quizTags;
            }
        }
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<QuizDto>>> Mine(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var connection = db.CreateConnection();
        var quizzes = await connection.QueryAsync<QuizDto>(
            new CommandDefinition(
                """
                select
                  q.id as Id,
                  q.slug as Slug,
                  q.author_id as AuthorId,
                  coalesce(nullif(concat_ws(' ', ap.first_name, ap.last_name), ''), ap.username, q.author_id::text) as AuthorName,
                  q.title as Title,
                  q.description as Description,
                  q.time_limit_minutes as TimeLimitMinutes,
                  q.major_id as MajorId,
                  m.name as MajorName,
                  q.course_id as CourseId,
                  c.name as CourseName,
                  q.lecturer_id as LecturerId,
                  l.full_name as LecturerName,
                  q.folder_id as FolderId,
                  q.visibility as Visibility,
                  q.access as Access,
                  q.allow_copy as AllowCopy,
                  q.version_number as VersionNumber,
                  q.has_been_updated as HasBeenUpdated,
                  q.cover_image_url as CoverImageUrl,
                  q.created_at as CreatedAt,
                  q.updated_at as UpdatedAt,
                  (select count(*) from public.questions where quiz_id = q.id and deleted_at is null) as QuestionCount
                from public.quizzes q
                left join public.profiles ap on q.author_id = ap.id
                left join public.lecturers l on q.lecturer_id = l.id
                left join public.majors m on q.major_id = m.id
                left join public.courses c on q.course_id = c.id
                where q.author_id = @UserId and q.deleted_at is null
                order by q.updated_at desc
                """,
                new { UserId = userId },
                cancellationToken: cancellationToken
            )
        );

        var result = quizzes.ToList();
        await PopulateTags(connection, result, cancellationToken);
        return Ok(result);
    }

    [HttpGet("mine/stats")]
    public async Task<ActionResult> MineStats(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var connection = db.CreateConnection();

        var stats = await connection.QuerySingleOrDefaultAsync(
            new CommandDefinition(
                """
                select
                  (select count(*) from public.attempts a
                   join public.quizzes q on a.quiz_id = q.id
                   where q.author_id = @UserId) as TotalAttempts,
                  (select count(distinct a.user_id) from public.attempts a
                   join public.quizzes q on a.quiz_id = q.id
                   where q.author_id = @UserId) as Participants,
                  (select count(*) from public.attempts a
                   join public.quizzes q on a.quiz_id = q.id
                   where q.author_id = @UserId and a.submitted_at is not null) as CompletedAttempts
                """,
                new { UserId = userId },
                cancellationToken: cancellationToken
            )
        );

        long totalAttempts = stats?.totalattempts > 0 ? Convert.ToInt64(stats.totalattempts) : 0;
        long participants = stats?.participants > 0 ? Convert.ToInt64(stats.participants) : 0;
        long completedAttempts = stats?.completedattempts > 0 ? Convert.ToInt64(stats.completedattempts) : 0;
        int completionRate = totalAttempts > 0 ? (int)Math.Round((decimal)completedAttempts / totalAttempts * 100) : 0;

        // 7-day chart: count per day-of-week (0=Sun … 6=Sat) for attempts started in the last 7 days
        var dailyRows = await connection.QueryAsync<(int DayOfWeek, int Count)>(
            new CommandDefinition(
                """
                select extract(dow from a.started_at)::int as DayOfWeek,
                       count(*)::int as Count
                from public.attempts a
                join public.quizzes q on a.quiz_id = q.id
                where q.author_id = @UserId
                  and a.started_at >= now() - interval '7 days'
                group by 1
                """,
                new { UserId = userId },
                cancellationToken: cancellationToken
            )
        );

        var chartData = new int[7];
        foreach (var row in dailyRows)
            chartData[row.DayOfWeek] = row.Count;

        return Ok(new
        {
            Attempts = (int)totalAttempts,
            Participants = (int)participants,
            CompletionRate = completionRate,
            ChartData = chartData
        });
    }

    [HttpGet("{identifier}")]
    public async Task<ActionResult<QuizDetailResponse>> Get(string identifier, CancellationToken cancellationToken)
    {
        await using var connection = db.CreateConnection();

        bool isGuid = Guid.TryParse(identifier, out var id);

        const string query = """
            select
              q.id as Id, q.slug as Slug, q.author_id as AuthorId,
              coalesce(nullif(concat_ws(' ', ap.first_name, ap.last_name), ''), ap.username, q.author_id::text) as AuthorName,
              q.title as Title, q.description as Description, q.time_limit_minutes as TimeLimitMinutes,
              q.major_id as MajorId, m.name as MajorName,
              q.course_id as CourseId, c.name as CourseName,
              q.lecturer_id as LecturerId, l.full_name as LecturerName,
              q.folder_id as FolderId, q.visibility as Visibility, q.access as Access,
              q.allow_copy as AllowCopy, q.version_number as VersionNumber, q.has_been_updated as HasBeenUpdated,
              q.cover_image_url as CoverImageUrl,
              q.created_at as CreatedAt, q.updated_at as UpdatedAt,
              (select count(*) from public.questions where quiz_id = q.id and deleted_at is null) as QuestionCount
            from public.quizzes q
            left join public.profiles ap on q.author_id = ap.id
            left join public.lecturers l on q.lecturer_id = l.id
            left join public.majors m on q.major_id = m.id
            left join public.courses c on q.course_id = c.id
            where (q.id = @Id or q.slug = @Slug) and q.deleted_at is null
            """;

        var quiz = await connection.QuerySingleOrDefaultAsync<QuizDto>(
            new CommandDefinition(query, new { Id = isGuid ? id : Guid.Empty, Slug = identifier }, cancellationToken: cancellationToken)
        );

        if (quiz is not null)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    insert into public.quiz_histories (user_id, quiz_id, last_opened_at)
                    select @UserId, @QuizId, now()
                    from public.profiles
                    where id = @UserId
                    on conflict (user_id, quiz_id)
                    do update set last_opened_at = now(), updated_at = now()
                    """,
                    new { UserId = User.GetUserId(), QuizId = quiz.Id },
                    cancellationToken: cancellationToken
                )
            );
        }

        if (quiz is null) return NotFound();
        await PopulateTags(connection, new List<QuizDto> { quiz }, cancellationToken);

        // Get related quizzes (same course, or same major)
        var related = (await connection.QueryAsync<QuizDto>(
            new CommandDefinition(
                $"""
                {query.Replace("(q.id = @Id or q.slug = @Slug)", "q.id != @QuizId")}
                and q.visibility = 'published'
                and (q.course_id = @CourseId or q.major_id = @MajorId)
                order by q.created_at desc
                limit 4
                """,
                new { QuizId = quiz.Id, CourseId = quiz.CourseId, MajorId = quiz.MajorId },
                cancellationToken: cancellationToken
            )
        )).ToList();

        await PopulateTags(connection, related, cancellationToken);

        return Ok(new QuizDetailResponse(quiz, related));
    }

    [HttpGet("{id:guid}/full")]
    public async Task<ActionResult<QuizFullDto>> GetFull(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var quiz = await GetQuizById(connection, null, id, cancellationToken);
        if (quiz is null || quiz.AuthorId != userId) return NotFound();

        await PopulateTags(connection, new List<QuizDto> { quiz }, cancellationToken);

        var questions = (await connection.QueryAsync<QuizQuestionDto>(
            new CommandDefinition(
                """
                select
                  id as Id, quiz_id as QuizId, content as Content,
                  question_type as QuestionType, order_index as OrderIndex,
                  image_url as ImageUrl
                from public.questions
                where quiz_id = @QuizId and deleted_at is null
                order by order_index asc
                """,
                new { QuizId = id },
                cancellationToken: cancellationToken
            ))).ToList();

        var questionIds = questions.Select(q => q.Id).ToArray();
        var options = new List<QuizOptionDto>();
        
        if (questionIds.Length > 0)
        {
            options = (await connection.QueryAsync<QuizOptionDto>(
                new CommandDefinition(
                    """
                    select
                      id as Id, question_id as QuestionId, content as Content,
                      is_correct as IsCorrect, order_index as OrderIndex
                    from public.options
                    where question_id = any(@QuestionIds) and deleted_at is null
                    order by order_index asc
                    """,
                    new { QuestionIds = questionIds },
                    cancellationToken: cancellationToken
                ))).ToList();
        }

        var fullQuestions = questions.Select(q => new QuizFullQuestionDto(
            q.Id,
            q.QuizId,
            q.Content,
            q.QuestionType,
            q.OrderIndex,
            q.ImageUrl,
            options.Where(o => o.QuestionId == q.Id).ToList()
        )).ToList();

        return Ok(new QuizFullDto(quiz, fullQuestions));
    }

    [HttpGet("{id:guid}/statistics")]
    public async Task<ActionResult> GetStatistics(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var connection = db.CreateConnection();
        
        var quiz = await GetQuizById(connection, null, id, cancellationToken);
        if (quiz is null || quiz.AuthorId != userId) return NotFound();

        var stats = await connection.QuerySingleOrDefaultAsync(
            new CommandDefinition(
                """
                select
                  (select count(*) from public.quiz_histories where quiz_id = @Id) as UsersClicked,
                  (select count(*) from public.attempts where quiz_id = @Id) as TotalAttempts,
                  (select count(distinct user_id) from public.attempts where quiz_id = @Id) as Participants,
                  (select count(*) from public.attempts where quiz_id = @Id and submitted_at is not null) as CompletedAttempts,
                  m.name as MajorName,
                  c.name as CourseName
                from public.quizzes q
                left join public.majors m on q.major_id = m.id
                left join public.courses c on q.course_id = c.id
                where q.id = @Id
                """,
                new { Id = id },
                cancellationToken: cancellationToken
            )
        );

        int usersClicked = stats?.usersclicked > 0 ? Convert.ToInt32(stats.usersclicked) : 0;
        int totalAttempts = stats?.totalattempts > 0 ? Convert.ToInt32(stats.totalattempts) : 0;
        int participants = stats?.participants > 0 ? Convert.ToInt32(stats.participants) : 0;
        int completedAttempts = stats?.completedattempts > 0 ? Convert.ToInt32(stats.completedattempts) : 0;

        int completionRate = totalAttempts > 0 ? (int)Math.Round((decimal)completedAttempts / totalAttempts * 100) : 0;

        return Ok(new
        {
            quiz.Title,
            quiz.MajorName,
            quiz.CourseName,
            quiz.CoverImageUrl,
            TotalAttempts = usersClicked, // Map UsersClicked to totalAttempts for frontend compatibility
            Participants = participants,
            CompletionRate = completionRate
        });
    }

    private static (string bucket, string path)? ParseStorageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!url.Contains("/storage/v1/object/public/"))
        {
            // Might be a private path (only path, no URL)
            if (url.StartsWith("questions/") || url.StartsWith("quizzes/"))
            {
                return (url.StartsWith("questions/") ? "private-assets" : "private-assets", url);
            }
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

    private static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-").Trim('-');
        return slug;
    }

    [HttpPost]
    public async Task<ActionResult<QuizDto>> Create([FromBody] CreateQuizRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var quizId = Guid.NewGuid();

        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    insert into public.quizzes (
                      id, slug, author_id, title, description, time_limit_minutes,
                      major_id, course_id, lecturer_id, folder_id, visibility,
                      access, allow_copy, cover_image_url, version_number, has_been_updated
                    )
                    values (
                      @Id, @Slug, @AuthorId, @Title, @Description, @TimeLimitMinutes,
                      @MajorId, @CourseId, @LecturerId, @FolderId, @Visibility,
                      @Access, @AllowCopy, @CoverImageUrl, 1, false
                    )
                    """,
                    new
                    {
                        Id = quizId,
                        Slug = $"{GenerateSlug(request.Title)}-{quizId.ToString("N")[..6]}",
                        AuthorId = userId,
                        request.Title,
                        request.Description,
                        request.TimeLimitMinutes,
                        request.MajorId,
                        request.CourseId,
                        request.LecturerId,
                        request.FolderId,
                        Visibility = request.Visibility,
                        Access = request.Access,
                        request.AllowCopy,
                        request.CoverImageUrl
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                )
            );

            if (request.Questions != null)
            {
                await SyncQuestionsAndOptions(connection, transaction, quizId, request.Questions, cancellationToken);
            }

            if (request.Tags != null)
            {
                await SyncTags(connection, transaction, quizId, request.Tags, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }


        var created = await GetQuizById(connection, null, quizId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { identifier = quizId }, created);
    }


    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQuizRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        try
        {
            // Get old cover image URL for cleanup
            var oldCoverImageUrl = await connection.QuerySingleOrDefaultAsync<string>(
                "select cover_image_url from public.quizzes where id = @Id and deleted_at is null",
                new { Id = id },
                transaction: transaction
            );

            var updated = await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    update public.quizzes
                    set
                      title = @Title,
                      slug = @Slug,
                      description = @Description,
                      time_limit_minutes = @TimeLimitMinutes,
                      major_id = @MajorId,
                      course_id = @CourseId,
                      lecturer_id = @LecturerId,
                      folder_id = @FolderId,
                      visibility = @Visibility,
                      access = @Access,
                      allow_copy = @AllowCopy,
                      cover_image_url = @CoverImageUrl,
                      updated_at = now(),
                      version_number = version_number + 1,
                      has_been_updated = true
                    where id = @Id and author_id = @AuthorId and deleted_at is null
                    """,
                    new
                    {
                        Id = id,
                        Slug = $"{GenerateSlug(request.Title)}-{id.ToString("N")[..6]}",
                        AuthorId = userId,
                        request.Title,
                        request.Description,
                        request.TimeLimitMinutes,
                        request.MajorId,
                        request.CourseId,
                        request.LecturerId,
                        request.FolderId,
                        request.Visibility,
                        request.Access,
                        request.AllowCopy,
                        request.CoverImageUrl
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                )
            );

            if (updated == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return NotFound();
            }

            if (request.Questions != null)
            {
                await SyncQuestionsAndOptions(connection, transaction, id, request.Questions, cancellationToken);
            }

            if (request.Tags != null)
            {
                await SyncTags(connection, transaction, id, request.Tags, cancellationToken);
            }

            // Cleanup old cover image if it changed
            if (oldCoverImageUrl != null && oldCoverImageUrl != request.CoverImageUrl)
            {
                var storageInfo = ParseStorageUrl(oldCoverImageUrl);
                if (storageInfo.HasValue)
                {
                    await storageService.DeleteImage(storageInfo.Value.bucket, storageInfo.Value.path);
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return NoContent();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task SyncQuestionsAndOptions(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, Guid quizId, List<SaveQuizQuestionRequest> questions, CancellationToken cancellationToken)
    {
        // 1. Get existing questions to know what to delete and which images to cleanup
        var existingQuestions = (await connection.QueryAsync<dynamic>(
            "select id, image_url from public.questions where quiz_id = @QuizId and deleted_at is null",
            new { QuizId = quizId },
            transaction: transaction
        )).Select(x => new { id = (Guid)x.id, image_url = (string?)x.image_url }).ToList();

        var keepQuestionIds = questions.Where(q => q.Id.HasValue).Select(q => q.Id!.Value).ToHashSet();
        
        // 2. Identify and hard-delete removed questions
        var questionsToDelete = existingQuestions.Where(q => !keepQuestionIds.Contains(q.id)).ToList();
        
        foreach (var q in questionsToDelete)
        {
            // Delete options first (hard delete)
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "delete from public.options where question_id = @QuestionId",
                    new { QuestionId = q.id },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                )
            );

            // Delete question (hard delete)
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "delete from public.questions where id = @Id",
                    new { Id = q.id },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                )
            );

            // Cleanup image from storage
            if (q.image_url != null)
            {
                var storageInfo = ParseStorageUrl(q.image_url);
                if (storageInfo.HasValue)
                {
                    await storageService.DeleteImage(storageInfo.Value.bucket, storageInfo.Value.path);
                }
            }
        }

        // 3. Process remaining/new questions
        foreach (var qReq in questions)
        {
            var questionId = qReq.Id ?? Guid.NewGuid();
            var existingQ = existingQuestions.FirstOrDefault(x => x.id == questionId);

            if (existingQ != null)
            {
                // If image changed, cleanup old one
                if (existingQ.image_url != null && existingQ.image_url != qReq.ImageUrl)
                {
                    var storageInfo = ParseStorageUrl(existingQ.image_url);
                    if (storageInfo.HasValue)
                    {
                        await storageService.DeleteImage(storageInfo.Value.bucket, storageInfo.Value.path);
                    }
                }

                await connection.ExecuteAsync(
                    new CommandDefinition(
                         "update public.questions set content = @Content, question_type = @QuestionType, order_index = @OrderIndex, image_url = @ImageUrl, updated_at = now(), deleted_at = null where id = @Id",
                        new { Id = questionId, qReq.Content, qReq.QuestionType, qReq.OrderIndex, qReq.ImageUrl },
                        transaction: transaction,
                        cancellationToken: cancellationToken
                    )
                );
            }
            else
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                         "insert into public.questions (id, quiz_id, content, question_type, order_index, image_url) values (@Id, @QuizId, @Content, @QuestionType, @OrderIndex, @ImageUrl)",
                        new { Id = questionId, QuizId = quizId, qReq.Content, qReq.QuestionType, qReq.OrderIndex, qReq.ImageUrl },
                        transaction: transaction,
                        cancellationToken: cancellationToken
                    )
                );
            }

            // Sync options for this question
            var keepOptionIds = qReq.Options.Where(o => o.Id.HasValue).Select(o => o.Id!.Value).ToArray();
            
            // Hard delete removed options
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "delete from public.options where question_id = @QuestionId and id != all(@KeepIds)",
                    new { QuestionId = questionId, KeepIds = keepOptionIds },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                )
            );

            foreach (var oReq in qReq.Options)
            {
                if (oReq.Id.HasValue)
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            "update public.options set content = @Content, is_correct = @IsCorrect, order_index = @OrderIndex, updated_at = now(), deleted_at = null where id = @Id",
                            new { Id = oReq.Id.Value, oReq.Content, oReq.IsCorrect, oReq.OrderIndex },
                            transaction: transaction,
                            cancellationToken: cancellationToken
                        )
                    );
                }
                else
                {
                    await connection.ExecuteAsync(
                        new CommandDefinition(
                            "insert into public.options (id, question_id, content, is_correct, order_index) values (@Id, @QuestionId, @Content, @IsCorrect, @OrderIndex)",
                            new { Id = Guid.NewGuid(), QuestionId = questionId, oReq.Content, oReq.IsCorrect, oReq.OrderIndex },
                            transaction: transaction,
                            cancellationToken: cancellationToken
                        )
                    );
                }
            }
        }
    }

    private async Task SyncTags(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction transaction, Guid quizId, List<string> tags, CancellationToken cancellationToken)
    {
        // Delete existing mappings
        await connection.ExecuteAsync(
            new CommandDefinition(
                "delete from public.quiz_tags where quiz_id = @QuizId",
                new { QuizId = quizId },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

        foreach (var tagName in tags.Distinct())
        {
            var tagId = await connection.QuerySingleOrDefaultAsync<Guid?>(
                new CommandDefinition(
                    "select id from public.tags where name = @Name",
                    new { Name = tagName },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                )
            );

            if (tagId == null)
            {
                tagId = Guid.NewGuid();
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        "insert into public.tags (id, name) values (@Id, @Name)",
                        new { Id = tagId, Name = tagName },
                        transaction: transaction,
                        cancellationToken: cancellationToken
                    )
                );
            }

            await connection.ExecuteAsync(
                new CommandDefinition(
                    "insert into public.quiz_tags (id, quiz_id, tag_id) values (@Id, @QuizId, @TagId)",
                    new { Id = Guid.NewGuid(), QuizId = quizId, TagId = tagId },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                )
            );
        }
    }

    private async Task<QuizDto?> GetQuizById(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction? transaction, Guid id, CancellationToken cancellationToken)
    {
        return await connection.QuerySingleOrDefaultAsync<QuizDto>(
            new CommandDefinition(
                """
                select
                  q.id as Id, q.slug as Slug, q.author_id as AuthorId, q.title as Title, q.description as Description,
                  q.time_limit_minutes as TimeLimitMinutes, q.major_id as MajorId, m.name as MajorName,
                  q.course_id as CourseId, c.name as CourseName,
                  q.lecturer_id as LecturerId, l.full_name as LecturerName, q.folder_id as FolderId, q.visibility as Visibility,
                  q.access as Access, q.allow_copy as AllowCopy, q.version_number as VersionNumber,
                  q.has_been_updated as HasBeenUpdated, q.cover_image_url as CoverImageUrl, q.created_at as CreatedAt, q.updated_at as UpdatedAt
                from public.quizzes q
                left join public.lecturers l on q.lecturer_id = l.id
                left join public.majors m on q.major_id = m.id
                left join public.courses c on q.course_id = c.id
                where q.id = @Id and q.deleted_at is null
                """,
                new { Id = id },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );
    }

    [HttpPatch("{id:guid}/folder")]
    public async Task<IActionResult> MoveToFolder(Guid id, [FromBody] MoveQuizRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var connection = db.CreateConnection();
        var affected = await connection.ExecuteAsync(
            "update public.quizzes set folder_id = @FolderId, updated_at = now() where id = @Id and author_id = @AuthorId and deleted_at is null",
            new { Id = id, AuthorId = userId, FolderId = request.FolderId }
        );
        return affected > 0 ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await using var connection = db.CreateConnection();
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "update public.quizzes set deleted_at = now() where id = @Id and author_id = @AuthorId and deleted_at is null",
                new { Id = id, AuthorId = userId },
                cancellationToken: cancellationToken
            )
        );
        if (affected == 0) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:guid}/copy")]
    public async Task<ActionResult> Copy(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var newQuizId = Guid.NewGuid();

        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        var sourceQuiz = await connection.QuerySingleOrDefaultAsync<QuizDto>(
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
                new { Id = id },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

        if (sourceQuiz is null)
        {
            return NotFound();
        }

        if (!sourceQuiz.AllowCopy || sourceQuiz.Visibility != "published")
        {
            return Forbid();
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                insert into public.quizzes (
                  id,
                  author_id,
                  title,
                  description,
                  time_limit_minutes,
                  major_id,
                  course_id,
                  lecturer_id,
                  folder_id,
                  visibility,
                  access,
                  allow_copy,
                  version_number,
                  has_been_updated,
                  cover_image_url
                )
                values (
                  @Id,
                  @AuthorId,
                  @Title,
                  @Description,
                  @TimeLimitMinutes,
                  @MajorId,
                  @CourseId,
                  @LecturerId,
                  null,
                  'draft',
                  'private',
                  false,
                  1,
                  false,
                  @CoverImageUrl
                )
                """,
                new
                {
                    Id = newQuizId,
                    AuthorId = userId,
                    Title = $"{sourceQuiz.Title} (copy)",
                    sourceQuiz.Description,
                    sourceQuiz.TimeLimitMinutes,
                    sourceQuiz.MajorId,
                    sourceQuiz.CourseId,
                    sourceQuiz.LecturerId,
                    sourceQuiz.CoverImageUrl
                },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

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
                new { QuizId = id },
                transaction: transaction,
                cancellationToken: cancellationToken
            ))).ToList();

        var questionIdMap = new Dictionary<Guid, Guid>();
        foreach (var q in questions)
        {
            var newQuestionId = Guid.NewGuid();
            questionIdMap[q.Id] = newQuestionId;
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    insert into public.questions (id, quiz_id, content, question_type, order_index, image_url)
                    values (@Id, @QuizId, @Content, @QuestionType, @OrderIndex, @ImageUrl)
                    """,
                    new
                    {
                        Id = newQuestionId,
                        QuizId = newQuizId,
                        q.Content,
                        q.QuestionType,
                        q.OrderIndex,
                        q.ImageUrl
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                )
            );
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
                    insert into public.options (id, question_id, content, is_correct, order_index)
                    values (@Id, @QuestionId, @Content, @IsCorrect, @OrderIndex)
                    """,
                    new
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = questionIdMap[o.QuestionId],
                        o.Content,
                        o.IsCorrect,
                        o.OrderIndex
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                )
            );
        }

        var existingTags = (await connection.QueryAsync<Guid>(
            new CommandDefinition(
                "select tag_id from public.quiz_tags where quiz_id = @OriginalQuizId",
                new { OriginalQuizId = id },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        )).ToList();

        foreach (var tagId in existingTags)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "insert into public.quiz_tags (id, quiz_id, tag_id) values (@Id, @QuizId, @TagId)",
                    new { Id = Guid.NewGuid(), QuizId = newQuizId, TagId = tagId },
                    transaction: transaction,
                    cancellationToken: cancellationToken
                )
            );
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                insert into public.quiz_copies (id, original_quiz_id, new_quiz_id, copied_by_user_id)
                values (@Id, @OriginalQuizId, @NewQuizId, @UserId)
                """,
                new { Id = Guid.NewGuid(), OriginalQuizId = id, NewQuizId = newQuizId, UserId = userId },
                transaction: transaction,
                cancellationToken: cancellationToken
            )
        );

        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { identifier = newQuizId }, new { id = newQuizId });
    }
}

