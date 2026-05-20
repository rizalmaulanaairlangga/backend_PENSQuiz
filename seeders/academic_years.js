const academicYears = [
  { label: "2023/2024 Ganjil", startDate: "2023-09-01", endDate: "2024-02-28", isActive: false },
  { label: "2023/2024 Genap", startDate: "2024-03-01", endDate: "2024-08-31", isActive: false },
  { label: "2024/2025 Ganjil", startDate: "2024-09-01", endDate: "2025-02-28", isActive: false },
  { label: "2024/2025 Genap", startDate: "2025-03-01", endDate: "2025-08-31", isActive: true }
];

async function seed(client) {
  console.log("Seeding academic years...");
  const seededYears = [];
  for (const ay of academicYears) {
    const res = await client.query(`
      INSERT INTO public.academic_years (label, start_date, end_date, is_active, created_at, updated_at)
      VALUES ($1, $2, $3, $4, NOW(), NOW())
      ON CONFLICT (label) DO UPDATE
      SET 
        start_date = EXCLUDED.start_date,
        end_date = EXCLUDED.end_date,
        is_active = EXCLUDED.is_active,
        updated_at = NOW()
      RETURNING id, label, is_active
    `, [ay.label, ay.startDate, ay.endDate, ay.isActive]);
    seededYears.push(res.rows[0]);
  }
  console.log(`Seeded ${seededYears.length} academic years.`);
  return seededYears;
}

module.exports = { seed };
