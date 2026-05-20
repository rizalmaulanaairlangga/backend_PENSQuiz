const quizzesData = [
  // AISHA (aisha)
  {
    username: "aisha",
    title: "Quiz 1 - Database Normalization",
    description: "Test your knowledge on 1NF, 2NF, 3NF, and BCNF normalization concepts.",
    timeLimitMinutes: 15,
    courseCode: "IT-201",
    lecturerStaffNumber: "196504041990031001", // Achmad Basuki
    visibility: "published",
    access: "public",
    slug: "quiz-1-database-normalization",
    tags: ["Database"],
    questions: [
      {
        content: "What is the main goal of database normalization?",
        questionType: "multiple_choice",
        options: [
          { content: "To reduce data redundancy and improve data integrity.", isCorrect: true },
          { content: "To increase database retrieval speed by duplicating data.", isCorrect: false },
          { content: "To convert SQL databases into NoSQL systems.", isCorrect: false },
          { content: "To automatically generate primary keys.", isCorrect: false }
        ]
      },
      {
        content: "Which of the following conditions must be met for a relation to be in Second Normal Form (2NF)?",
        questionType: "checkbox",
        options: [
          { content: "It must already be in First Normal Form (1NF).", isCorrect: true },
          { content: "All non-prime attributes must be fully functionally dependent on the primary key (no partial dependency).", isCorrect: true },
          { content: "It must have no transitive dependencies.", isCorrect: false },
          { content: "It must contain at least three foreign keys.", isCorrect: false }
        ]
      }
    ]
  },
  {
    username: "aisha",
    title: "Web Development - HTTP Protocol Essentials",
    description: "Understanding requests, responses, status codes, and HTTP methods.",
    timeLimitMinutes: 10,
    courseCode: "IT-301",
    lecturerStaffNumber: "198803032015042003", // Rina Yuliana
    visibility: "published",
    access: "private",
    slug: "web-dev-http-protocol-essentials",
    tags: ["Web Development"],
    questions: [
      {
        content: "Which HTTP status code represents 'Internal Server Error'?",
        questionType: "multiple_choice",
        options: [
          { content: "500", isCorrect: true },
          { content: "400", isCorrect: false },
          { content: "404", isCorrect: false },
          { content: "403", isCorrect: false }
        ]
      },
      {
        content: "What is the difference between GET and POST requests?",
        questionType: "multiple_choice",
        options: [
          { content: "GET is idempotent and parameters are visible in the URL; POST is not idempotent and sends data in the request body.", isCorrect: true },
          { content: "GET request data is secure, while POST request data is visible in plain text.", isCorrect: false },
          { content: "GET is used only for CSS, while POST is used for HTML.", isCorrect: false },
          { content: "There is no difference.", isCorrect: false }
        ]
      }
    ]
  },

  // NICOLAUS (nicolaus)
  {
    username: "nicolaus",
    title: "Arduino Programming & GPIO Basics",
    description: "Test your understanding of Arduino pins, analog vs digital I/O, and simple sensors.",
    timeLimitMinutes: 12,
    courseCode: "EL-201",
    lecturerStaffNumber: "198502022010121002", // Dwi Susanto
    visibility: "published",
    access: "public",
    slug: "arduino-programming-gpio-basics",
    tags: ["Electronics", "Programming"],
    questions: [
      {
        content: "What is the range of values returned by analogRead() on a standard Arduino Uno?",
        questionType: "multiple_choice",
        options: [
          { content: "0 to 1023", isCorrect: true },
          { content: "0 to 255", isCorrect: false },
          { content: "-128 to 127", isCorrect: false },
          { content: "0 to 5", isCorrect: false }
        ]
      },
      {
        content: "Which of the following pins on Arduino Uno support hardware PWM output?",
        questionType: "checkbox",
        options: [
          { content: "Pin 3", isCorrect: true },
          { content: "Pin 5", isCorrect: true },
          { content: "Pin 6", isCorrect: true },
          { content: "Pin 12", isCorrect: false }
        ]
      }
    ]
  },
  {
    username: "nicolaus",
    title: "Ohm's Law and DC Circuits",
    description: "Fundamental circuit theory covering current, voltage, resistance, and Kirchhoff's Laws.",
    timeLimitMinutes: 10,
    courseCode: "EL-101",
    lecturerStaffNumber: "198502022010121002", // Dwi Susanto
    visibility: "published",
    access: "private",
    slug: "ohms-law-and-dc-circuits",
    tags: ["Electronics", "Physics"],
    questions: [
      {
        content: "If a 12V voltage source is connected across a 4 Ohm resistor, what is the current flowing through the circuit?",
        questionType: "multiple_choice",
        options: [
          { content: "3A", isCorrect: true },
          { content: "0.33A", isCorrect: false },
          { content: "48A", isCorrect: false },
          { content: "8A", isCorrect: false }
        ]
      },
      {
        content: "According to Kirchhoff's Current Law (KCL), what is the sum of currents entering a junction?",
        questionType: "multiple_choice",
        options: [
          { content: "Equal to the sum of currents leaving the junction.", isCorrect: true },
          { content: "Always zero regardless of outputs.", isCorrect: false },
          { content: "Dependent on the voltage of the junction.", isCorrect: false },
          { content: "Twice the current leaving the junction.", isCorrect: false }
        ]
      }
    ]
  },

  // RIZAL (rizal)
  {
    username: "rizal",
    title: "Java OOP Principles",
    description: "Quick check of Object-Oriented Programming principles: Inheritance, Encapsulation, Polymorphism, Abstraction.",
    timeLimitMinutes: 15,
    courseCode: "IT-101",
    lecturerStaffNumber: "197001012000031001", // Anang Budikarso
    visibility: "published",
    access: "public",
    slug: "java-oop-principles",
    tags: ["OOP", "Programming"],
    questions: [
      {
        content: "Which keyword in Java is used to inherit a class?",
        questionType: "multiple_choice",
        options: [
          { content: "extends", isCorrect: true },
          { content: "implements", isCorrect: false },
          { content: "inherits", isCorrect: false },
          { content: "interface", isCorrect: false }
        ]
      },
      {
        content: "Which of the following are pillars of Object-Oriented Programming?",
        questionType: "checkbox",
        options: [
          { content: "Encapsulation", isCorrect: true },
          { content: "Inheritance", isCorrect: true },
          { content: "Polymorphism", isCorrect: true },
          { content: "Compilation", isCorrect: false }
        ]
      }
    ]
  },
  {
    username: "rizal",
    title: "SQL Join Types",
    description: "Check understanding of INNER, LEFT, RIGHT, and FULL OUTER joins in relational databases.",
    timeLimitMinutes: 10,
    courseCode: "IT-201",
    lecturerStaffNumber: "196504041990031001", // Achmad Basuki
    visibility: "published",
    access: "private",
    slug: "sql-join-types",
    tags: ["Database"],
    questions: [
      {
        content: "Which type of JOIN returns only the matching rows from both tables?",
        questionType: "multiple_choice",
        options: [
          { content: "INNER JOIN", isCorrect: true },
          { content: "LEFT JOIN", isCorrect: false },
          { content: "RIGHT JOIN", isCorrect: false },
          { content: "FULL OUTER JOIN", isCorrect: false }
        ]
      },
      {
        content: "What will a LEFT JOIN return if there is no match in the right table?",
        questionType: "multiple_choice",
        options: [
          { content: "All rows from the left table and NULL values for the right table columns.", isCorrect: true },
          { content: "Only the matching rows, dropping unmatched left rows.", isCorrect: false },
          { content: "An SQL syntax error.", isCorrect: false },
          { content: "Empty result set.", isCorrect: false }
        ]
      }
    ]
  },

  // AHMAD (ahmad)
  {
    username: "ahmad",
    title: "Data Structures Basics",
    description: "Test knowledge on linear data structures such as Arrays, Stacks, Queues, and Linked Lists.",
    timeLimitMinutes: 10,
    courseCode: "IT-101",
    lecturerStaffNumber: "197001012000031001", // Anang Budikarso
    visibility: "published",
    access: "public",
    slug: "data-structures-basics",
    tags: ["Programming"],
    questions: [
      {
        content: "Which data structure operates on a Last-In, First-Out (LIFO) basis?",
        questionType: "multiple_choice",
        options: [
          { content: "Stack", isCorrect: true },
          { content: "Queue", isCorrect: false },
          { content: "Array", isCorrect: false },
          { content: "Linked List", isCorrect: false }
        ]
      },
      {
        content: "Which of the following statements about Arrays are correct?",
        questionType: "checkbox",
        options: [
          { content: "Element lookup by index is an O(1) operation.", isCorrect: true },
          { content: "Array size is generally fixed upon initialization.", isCorrect: true },
          { content: "Elements can only be added at the beginning of the array in O(1) time.", isCorrect: false },
          { content: "Array elements are stored in random memory locations.", isCorrect: false }
        ]
      }
    ]
  },
  {
    username: "ahmad",
    title: "Probability & Statistics Core",
    description: "Fundamental concepts of Mean, Median, Mode, Variance, and Standard Deviation.",
    timeLimitMinutes: 15,
    courseCode: "SDT-101",
    lecturerStaffNumber: "196504041990031001", // Achmad Basuki
    visibility: "published",
    access: "private",
    slug: "probability-and-statistics-core",
    tags: ["Mathematics", "Statistics"],
    questions: [
      {
        content: "What is the value that appears most frequently in a data set?",
        questionType: "multiple_choice",
        options: [
          { content: "Mode", isCorrect: true },
          { content: "Mean", isCorrect: false },
          { content: "Median", isCorrect: false },
          { content: "Standard Deviation", isCorrect: false }
        ]
      },
      {
        content: "If a data set has values [2, 4, 4, 6, 9], what is the Median?",
        questionType: "multiple_choice",
        options: [
          { content: "4", isCorrect: true },
          { content: "5", isCorrect: false },
          { content: "6", isCorrect: false },
          { content: "4.4", isCorrect: false }
        ]
      }
    ]
  }
];

async function seed(client) {
  console.log("Seeding quizzes, questions, and options...");

  // 1. Fetch maps
  const profilesRes = await client.query("SELECT id, username, major_id FROM public.profiles");
  const profilesMap = {};
  profilesRes.rows.forEach(p => {
    profilesMap[p.username] = { id: p.id, majorId: p.major_id };
  });

  const coursesRes = await client.query("SELECT id, code FROM public.courses");
  const coursesMap = {};
  coursesRes.rows.forEach(c => {
    coursesMap[c.code] = c.id;
  });

  const lecturersRes = await client.query("SELECT id, staff_number FROM public.lecturers");
  const lecturersMap = {};
  lecturersRes.rows.forEach(l => {
    lecturersMap[l.staff_number] = l.id;
  });

  const foldersRes = await client.query("SELECT id, user_id, name FROM public.folders");
  const foldersMap = {};
  foldersRes.rows.forEach(f => {
    foldersMap[f.user_id] = f.id;
  });

  const tagsRes = await client.query("SELECT id, name FROM public.tags");
  const tagsMap = {};
  tagsRes.rows.forEach(t => {
    tagsMap[t.name] = t.id;
  });

  let seededCount = 0;

  for (const quiz of quizzesData) {
    const profile = profilesMap[quiz.username];
    if (!profile) {
      console.warn(`Warning: User profile ${quiz.username} not found. Skipping quiz "${quiz.title}".`);
      continue;
    }

    const authorId = profile.id;
    const majorId = profile.majorId;
    const courseId = coursesMap[quiz.courseCode];
    const lecturerId = lecturersMap[quiz.lecturerStaffNumber];
    const folderId = foldersMap[authorId];

    if (!courseId || !lecturerId || !folderId) {
      console.warn(`Warning: Missing dependencies for quiz "${quiz.title}" (Course: ${courseId}, Lecturer: ${lecturerId}, Folder: ${folderId}). Skipping.`);
      continue;
    }

    // Defensive check to avoid duplicate key issues if the database wasn't cleared
    const existCheck = await client.query(
      "SELECT id FROM public.quizzes WHERE author_id = $1 AND title = $2",
      [authorId, quiz.title]
    );
    if (existCheck.rows.length > 0) {
      const existingQuizId = existCheck.rows[0].id;
      // Cascade delete existing quiz and its cascade children
      await client.query("DELETE FROM public.quizzes WHERE id = $1", [existingQuizId]);
    }

    // Insert Quiz
    const quizRes = await client.query(`
      INSERT INTO public.quizzes (
        author_id, title, description, time_limit_minutes, major_id, 
        course_id, lecturer_id, folder_id, visibility, access, slug,
        created_at, updated_at
      ) VALUES (
        $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, NOW(), NOW()
      ) RETURNING id
    `, [
      authorId, quiz.title, quiz.description, quiz.timeLimitMinutes, majorId,
      courseId, lecturerId, folderId, quiz.visibility, quiz.access, quiz.slug
    ]);
    const quizId = quizRes.rows[0].id;

    // Insert Questions
    for (let i = 0; i < quiz.questions.length; i++) {
      const question = quiz.questions[i];
      const questionRes = await client.query(`
        INSERT INTO public.questions (
          quiz_id, content, question_type, order_index, created_at, updated_at
        ) VALUES (
          $1, $2, $3, $4, NOW(), NOW()
        ) RETURNING id
      `, [quizId, question.content, question.questionType, i]);
      const questionId = questionRes.rows[0].id;

      // Insert Options
      for (let j = 0; j < question.options.length; j++) {
        const option = question.options[j];
        await client.query(`
          INSERT INTO public.options (
            question_id, content, is_correct, order_index, created_at, updated_at
          ) VALUES (
            $1, $2, $3, $4, NOW(), NOW()
          )
        `, [questionId, option.content, option.isCorrect, j]);
      }
    }

    // Insert Quiz Tags
    if (quiz.tags && quiz.tags.length > 0) {
      for (const tagName of quiz.tags) {
        const tagId = tagsMap[tagName];
        if (tagId) {
          await client.query(`
            INSERT INTO public.quiz_tags (quiz_id, tag_id, created_at)
            VALUES ($1, $2, NOW())
          `, [quizId, tagId]);
        } else {
          console.warn(`Warning: Tag "${tagName}" not found. Skipping tag association for quiz "${quiz.title}".`);
        }
      }
    }

    seededCount++;
  }

  console.log(`Seeded ${seededCount} quizzes with questions and options successfully.`);
}

module.exports = { seed };
