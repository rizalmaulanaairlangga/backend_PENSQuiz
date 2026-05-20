const { getClient } = require('./db');
const majors = require('./majors');
const academicYears = require('./academic_years');
const tags = require('./tags');
const lecturers = require('./lecturers');
const courses = require('./courses');
const classes = require('./classes');
const users = require('./users');
const folders = require('./folders');
const quizzes = require('./quizzes');
const attempts = require('./attempts');

async function clearDatabase(client) {
  console.log("Clearing existing data from database...");
  
  const tables = [
    'public.attempt_answer_options',
    'public.attempt_answers',
    'public.attempts',
    'public.classes',
    'public.courses',
    'public.folders',
    'public.lecturers',
    'public.majors',
    'public.options',
    'public.profiles',
    'public.questions',
    'public.quiz_copies',
    'public.quiz_histories',
    'public.quiz_snapshots',
    'public.quiz_tags',
    'public.quiz_topics',
    'public.quizzes',
    'public.snapshot_options',
    'public.snapshot_questions',
    'public.tags',
    'public.topics',
    'public.user_quiz_stats'
  ];
  
  // Truncate public tables cascading down to foreign keys
  await client.query(`TRUNCATE ${tables.join(', ')} CASCADE;`);
  console.log("Public tables truncated successfully.");

  // Delete specific seeded student users from auth.users
  const seededEmails = [
    'aisha@student.pens.ac.id',
    'nicolaus@student.pens.ac.id',
    'rizal@student.pens.ac.id',
    'ahmad@student.pens.ac.id',
    'seeder1@student.pens.ac.id',
    'seeder2@student.pens.ac.id',
    'seeder3@student.pens.ac.id'
  ];
  
  await client.query(`
    DELETE FROM auth.users 
    WHERE email = ANY($1)
  `, [seededEmails]);
  console.log("Seeded auth users deleted successfully.");
}

async function runAll() {
  const shouldClear = process.argv.includes('--clear') || process.argv.includes('--clear-only');
  const clearOnly = process.argv.includes('--clear-only');

  const client = await getClient();

  try {
    if (shouldClear) {
      await clearDatabase(client);
      console.log();
      if (clearOnly) {
        console.log("Database cleared successfully!");
        return;
      }
    }

    console.log("Starting database seeding process...\n");

    // 1. Majors (independent)
    await majors.seed(client);
    console.log();
    
    // 2. Academic Years (independent)
    await academicYears.seed(client);
    console.log();
    
    // 3. Tags (independent)
    await tags.seed(client);
    console.log();
    
    // 4. Lecturers (depends on Majors)
    await lecturers.seed(client);
    console.log();
    
    // 5. Courses (depends on Majors)
    await courses.seed(client);
    console.log();
    
    // 6. Classes (depends on Courses, Academic Years, and Lecturers)
    await classes.seed(client);
    console.log();
    
    // 7. Users (depends on Majors)
    await users.seed(client);
    console.log();

    // 8. Folders (depends on Users/Profiles)
    await folders.seed(client);
    console.log();

    // 9. Quizzes (depends on Users/Profiles, Courses, Lecturers, and Folders)
    await quizzes.seed(client);
    console.log();

    // 10. Attempts (depends on Users/Profiles, Quizzes, Questions, and Options)
    await attempts.seed(client);
    console.log();
    
    console.log("All seeders executed successfully!");
  } catch (error) {
    console.error("Database operation failed with error:", error);
    process.exit(1);
  } finally {
    await client.end();
  }
}

runAll().catch(err => {
  console.error(err);
  process.exit(1);
});


