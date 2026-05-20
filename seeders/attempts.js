const { v4: uuidv4 } = require('uuid');

async function ensureQuizSnapshot(client, quizId, versionNumber) {
  // Check if snapshot already exists
  const existRes = await client.query(
    "SELECT id FROM public.quiz_snapshots WHERE quiz_id = $1 AND version_number = $2",
    [quizId, versionNumber]
  );
  
  if (existRes.rows.length > 0) {
    return existRes.rows[0].id;
  }

  const snapshotId = uuidv4();
  await client.query(
    "INSERT INTO public.quiz_snapshots (id, quiz_id, version_number, created_at) VALUES ($1, $2, $3, NOW())",
    [snapshotId, quizId, versionNumber]
  );

  // Load questions for the quiz
  const qRes = await client.query(
    "SELECT id, content, question_type, order_index FROM public.questions WHERE quiz_id = $1 AND deleted_at IS NULL ORDER BY order_index ASC",
    [quizId]
  );

  const questionIdMap = {};
  for (const q of qRes.rows) {
    const snapshotQuestionId = uuidv4();
    questionIdMap[q.id] = snapshotQuestionId;
    await client.query(`
      INSERT INTO public.snapshot_questions (
        id, snapshot_id, original_question_id, content, question_type, order_index, explanation
      ) VALUES ($1, $2, $3, $4, $5, $6, NULL)
    `, [snapshotQuestionId, snapshotId, q.id, q.content, q.question_type, q.order_index]);
  }

  if (qRes.rows.length > 0) {
    // Load options for the questions
    const qIds = qRes.rows.map(r => r.id);
    const oRes = await client.query(`
      SELECT id, question_id, content, is_correct, order_index 
      FROM public.options 
      WHERE question_id = ANY($1) AND deleted_at IS NULL 
      ORDER BY order_index ASC
    `, [qIds]);

    for (const o of oRes.rows) {
      await client.query(`
        INSERT INTO public.snapshot_options (
          id, snapshot_question_id, original_option_id, content, is_correct, order_index
        ) VALUES ($1, $2, $3, $4, $5, $6)
      `, [uuidv4(), questionIdMap[o.question_id], o.id, o.content, o.is_correct, o.order_index]);
    }
  }

  return snapshotId;
}

async function seed(client) {
  console.log("Seeding attempts and histories for seeder accounts...");

  // 1. Fetch seeder accounts
  const seedersRes = await client.query(`
    SELECT id, username FROM public.profiles 
    WHERE username IN ('aisha', 'nicolaus', 'rizal', 'ahmad')
  `);
  const seederProfiles = seedersRes.rows;

  if (seederProfiles.length === 0) {
    console.error("Error: Seeder accounts ('aisha', 'nicolaus', 'rizal', 'ahmad') not found in public.profiles!");
    return;
  }

  // 2. Fetch public quizzes with authors
  const quizzesRes = await client.query(`
    SELECT q.id, q.title, q.version_number, p.username as author_username 
    FROM public.quizzes q
    JOIN public.profiles p ON q.author_id = p.id
    WHERE q.visibility = 'published' AND q.access = 'public'
  `);
  const publicQuizzes = quizzesRes.rows;

  if (publicQuizzes.length < 3) {
    console.error("Error: Not enough public quizzes found to satisfy seeder requirements!");
    return;
  }

  let attemptCount = 0;
  let historyCount = 0;

  for (const seeder of seederProfiles) {
    console.log(`Processing attempts for user: ${seeder.username}...`);

    const otherQuizzes = publicQuizzes.filter(q => q.author_username !== seeder.username);
    
    // Assign half to completed, half to incomplete
    const mid = Math.floor(otherQuizzes.length / 2);
    const completedQuizzes = otherQuizzes.slice(0, mid);
    const incompleteQuizzes = otherQuizzes.slice(mid);

    // A. Seed Completed Quizzes (between 2 and 5)
    for (const quiz of completedQuizzes) {
      const snapshotId = await ensureQuizSnapshot(client, quiz.id, quiz.version_number);

      // 1. Update quiz history (viewed details)
      await client.query(`
        INSERT INTO public.quiz_histories (user_id, quiz_id, last_opened_at, created_at, updated_at)
        VALUES ($1, $2, NOW() - interval '1 hour', NOW(), NOW())
        ON CONFLICT (user_id, quiz_id) DO UPDATE
        SET last_opened_at = EXCLUDED.last_opened_at, updated_at = NOW()
      `, [seeder.id, quiz.id]);
      historyCount++;

      // 2. Insert attempt
      const attemptId = uuidv4();
      const startedAt = new Date(Date.now() - 3600000); // 1 hour ago
      const submittedAt = new Date(startedAt.getTime() + 600000); // 10 minutes later

      await client.query(`
        INSERT INTO public.attempts (id, user_id, quiz_id, snapshot_id, started_at, submitted_at, duration_seconds, score, created_at, updated_at)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8, NOW(), NOW())
      `, [attemptId, seeder.id, quiz.id, snapshotId, startedAt, submittedAt, 600, 100.0]);

      // 3. Load snapshot questions and options to answer them
      const sqRes = await client.query(
        "SELECT id FROM public.snapshot_questions WHERE snapshot_id = $1",
        [snapshotId]
      );

      for (const sq of sqRes.rows) {
        const attemptAnswerId = uuidv4();
        await client.query(`
          INSERT INTO public.attempt_answers (id, attempt_id, snapshot_question_id, created_at, updated_at)
          VALUES ($1, $2, $3, NOW(), NOW())
        `, [attemptAnswerId, attemptId, sq.id]);

        // Get correct options for this question
        const soRes = await client.query(
          "SELECT id FROM public.snapshot_options WHERE snapshot_question_id = $1 AND is_correct = true",
          [sq.id]
        );

        for (const so of soRes.rows) {
          await client.query(`
            INSERT INTO public.attempt_answer_options (id, attempt_answer_id, snapshot_option_id)
            VALUES ($1, $2, $3)
          `, [uuidv4(), attemptAnswerId, so.id]);
        }
      }
      attemptCount++;
    }

    // B. Seed Incomplete Quizzes (between 1 and 3)
    for (const quiz of incompleteQuizzes) {
      const snapshotId = await ensureQuizSnapshot(client, quiz.id, quiz.version_number);

      // 1. Update quiz history (viewed details)
      await client.query(`
        INSERT INTO public.quiz_histories (user_id, quiz_id, last_opened_at, created_at, updated_at)
        VALUES ($1, $2, NOW(), NOW(), NOW())
        ON CONFLICT (user_id, quiz_id) DO UPDATE
        SET last_opened_at = EXCLUDED.last_opened_at, updated_at = NOW()
      `, [seeder.id, quiz.id]);
      historyCount++;

      // 2. Insert incomplete attempt (no submitted_at, no score)
      const attemptId = uuidv4();
      const startedAt = new Date(Date.now() - 600000); // 10 minutes ago

      await client.query(`
        INSERT INTO public.attempts (id, user_id, quiz_id, snapshot_id, started_at, submitted_at, duration_seconds, score, created_at, updated_at)
        VALUES ($1, $2, $3, $4, $5, NULL, NULL, NULL, NOW(), NOW())
      `, [attemptId, seeder.id, quiz.id, snapshotId, startedAt]);

      // 3. Answer only the first question to make it look in-progress (incomplete)
      const sqRes = await client.query(
        "SELECT id FROM public.snapshot_questions WHERE snapshot_id = $1 ORDER BY order_index ASC LIMIT 1",
        [snapshotId]
      );

      if (sqRes.rows.length > 0) {
        const sq = sqRes.rows[0];
        const attemptAnswerId = uuidv4();
        await client.query(`
          INSERT INTO public.attempt_answers (id, attempt_id, snapshot_question_id, created_at, updated_at)
          VALUES ($1, $2, $3, NOW(), NOW())
        `, [attemptAnswerId, attemptId, sq.id]);

        // Get options for this question and pick the first one
        const soRes = await client.query(
          "SELECT id FROM public.snapshot_options WHERE snapshot_question_id = $1 LIMIT 1",
          [sq.id]
        );

        if (soRes.rows.length > 0) {
          const so = soRes.rows[0];
          await client.query(`
            INSERT INTO public.attempt_answer_options (id, attempt_answer_id, snapshot_option_id)
            VALUES ($1, $2, $3)
          `, [uuidv4(), attemptAnswerId, so.id]);
        }
      }
      attemptCount++;
    }
  }

  console.log(`Successfully seeded ${attemptCount} attempts and ${historyCount} history records.`);
}

module.exports = { seed };
