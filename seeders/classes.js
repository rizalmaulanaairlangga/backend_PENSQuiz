const classes = [
  { name: "1 D4 IT A", courseCode: "IT-101", academicYearLabel: "2024/2025 Ganjil", semester: 1, lecturerStaffNumber: "198803032015042003" },
  { name: "2 D4 IT B", courseCode: "IT-201", academicYearLabel: "2024/2025 Ganjil", semester: 3, lecturerStaffNumber: "196504041990031001" },
  { name: "1 D3 EL A", courseCode: "EL-101", academicYearLabel: "2024/2025 Ganjil", semester: 1, lecturerStaffNumber: "198502022010121002" }
];

async function seed(client) {
  console.log("Seeding classes...");

  // Maps
  const coursesRes = await client.query("SELECT id, code FROM public.courses");
  const coursesMap = {};
  coursesRes.rows.forEach(row => coursesMap[row.code] = row.id);

  const ayRes = await client.query("SELECT id, label FROM public.academic_years");
  const ayMap = {};
  ayRes.rows.forEach(row => ayMap[row.label] = row.id);

  const lecRes = await client.query("SELECT id, staff_number FROM public.lecturers");
  const lecMap = {};
  lecRes.rows.forEach(row => lecMap[row.staff_number] = row.id);

  const seededClasses = [];
  for (const cls of classes) {
    const courseId = coursesMap[cls.courseCode];
    const academicYearId = ayMap[cls.academicYearLabel];
    const lecturerId = lecMap[cls.lecturerStaffNumber];

    if (!courseId || !academicYearId || !lecturerId) {
      console.warn(`Warning: Missing dependencies for class ${cls.name}. Skipping.`);
      continue;
    }

    // Check check-and-insert/update
    const existCheck = await client.query(
      "SELECT id FROM public.classes WHERE name = $1 AND course_id = $2 AND academic_year_id = $3",
      [cls.name, courseId, academicYearId]
    );

    let res;
    if (existCheck.rows.length > 0) {
      const classId = existCheck.rows[0].id;
      res = await client.query(`
        UPDATE public.classes
        SET 
          semester = $1,
          lecturer_id = $2,
          updated_at = NOW()
        WHERE id = $3
        RETURNING id, name
      `, [cls.semester, lecturerId, classId]);
    } else {
      res = await client.query(`
        INSERT INTO public.classes (name, course_id, academic_year_id, semester, lecturer_id, created_at, updated_at)
        VALUES ($1, $2, $3, $4, $5, NOW(), NOW())
        RETURNING id, name
      `, [cls.name, courseId, academicYearId, cls.semester, lecturerId]);
    }
    seededClasses.push(res.rows[0]);
  }
  console.log(`Seeded ${seededClasses.length} classes.`);
  return seededClasses;
}

module.exports = { seed };
