const courses = [
  { code: "IT-101", name: "Pemrograman Dasar", credits: 3, majorCode: "TI" },
  { code: "IT-201", name: "Basis Data", credits: 4, majorCode: "TI" },
  { code: "IT-301", name: "Pemrograman Web", credits: 3, majorCode: "TI" },
  { code: "EL-101", name: "Rangkaian Elektrik", credits: 3, majorCode: "EL" },
  { code: "EL-201", name: "Mikrokontroler", credits: 4, majorCode: "EL" },
  { code: "SDT-101", name: "Statistika Dasar", credits: 3, majorCode: "SDT" }
];

async function seed(client) {
  console.log("Seeding courses...");

  // Get majors mapping
  const majorsRes = await client.query("SELECT id, code FROM public.majors");
  const majorsMap = {};
  majorsRes.rows.forEach(row => {
    majorsMap[row.code] = row.id;
  });

  const seededCourses = [];
  for (const course of courses) {
    const majorId = majorsMap[course.majorCode];
    if (!majorId) {
      console.warn(`Warning: Major code ${course.majorCode} not found for course ${course.name}. Skipping.`);
      continue;
    }

    const res = await client.query(`
      INSERT INTO public.courses (code, name, major_id, credits, created_at, updated_at)
      VALUES ($1, $2, $3, $4, NOW(), NOW())
      ON CONFLICT (code) DO UPDATE
      SET 
        name = EXCLUDED.name,
        major_id = EXCLUDED.major_id,
        credits = EXCLUDED.credits,
        updated_at = NOW()
      RETURNING id, code, name
    `, [course.code, course.name, majorId, course.credits]);
    seededCourses.push(res.rows[0]);
  }
  console.log(`Seeded ${seededCourses.length} courses.`);
  return seededCourses;
}

module.exports = { seed };
