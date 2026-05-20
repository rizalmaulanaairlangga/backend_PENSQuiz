const lecturers = [
  { staffNumber: "197001012000031001", fullName: "Dr. Ir. Anang Budikarso, M.T.", email: "anang@pens.ac.id", majorCode: "TI" },
  { staffNumber: "198502022010121002", fullName: "Dwi Susanto, S.S.T., M.T.", email: "dwisusanto@pens.ac.id", majorCode: "EL" },
  { staffNumber: "198803032015042003", fullName: "Rina Yuliana, S.T., M.T.", email: "rina@pens.ac.id", majorCode: "TI" },
  { staffNumber: "196504041990031001", fullName: "Achmad Basuki, Ph.D.", email: "abas@pens.ac.id", majorCode: "TI" }
];

async function seed(client) {
  console.log("Seeding lecturers...");
  
  // Get majors mapping
  const majorsRes = await client.query("SELECT id, code FROM public.majors");
  const majorsMap = {};
  majorsRes.rows.forEach(row => {
    majorsMap[row.code] = row.id;
  });

  const seededLecturers = [];
  for (const lec of lecturers) {
    const majorId = majorsMap[lec.majorCode];
    if (!majorId) {
      console.warn(`Warning: Major code ${lec.majorCode} not found for lecturer ${lec.fullName}. Skipping.`);
      continue;
    }

    const res = await client.query(`
      INSERT INTO public.lecturers (staff_number, full_name, email, major_id, created_at, updated_at)
      VALUES ($1, $2, $3, $4, NOW(), NOW())
      ON CONFLICT (staff_number) DO UPDATE
      SET 
        full_name = EXCLUDED.full_name,
        email = EXCLUDED.email,
        major_id = EXCLUDED.major_id,
        updated_at = NOW()
      RETURNING id, staff_number, full_name
    `, [lec.staffNumber, lec.fullName, lec.email, majorId]);
    seededLecturers.push(res.rows[0]);
  }
  console.log(`Seeded ${seededLecturers.length} lecturers.`);
  return seededLecturers;
}

module.exports = { seed };
