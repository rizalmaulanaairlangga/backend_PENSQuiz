const majors = [
  { code: "TI", name: "Teknik Informatika" },
  { code: "EL", name: "Teknik Elektronika" },
  { code: "TEL", name: "Teknik Telekomunikasi" },
  { code: "EI", name: "Teknik Elektro Industri" },
  { code: "IT", name: "Teknik Komputer" },
  { code: "TRM", name: "Teknologi Rekayasa Multimedia" },
  { code: "TRI", name: "Teknologi Rekayasa Internet" },
  { code: "SDT", name: "Sains Data Terapan" }
];

async function seed(client) {
  console.log("Seeding majors...");
  const seededMajors = [];
  for (const major of majors) {
    const res = await client.query(`
      INSERT INTO public.majors (code, name, created_at, updated_at)
      VALUES ($1, $2, NOW(), NOW())
      ON CONFLICT (name) DO UPDATE
      SET code = EXCLUDED.code, updated_at = NOW()
      RETURNING id, code, name
    `, [major.code, major.name]);
    seededMajors.push(res.rows[0]);
  }
  console.log(`Seeded ${seededMajors.length} majors.`);
  return seededMajors;
}

module.exports = { seed };
