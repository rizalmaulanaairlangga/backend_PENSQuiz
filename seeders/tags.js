const tags = [
  { name: "Programming" },
  { name: "Database" },
  { name: "Networking" },
  { name: "Web Development" },
  { name: "Calculus" },
  { name: "Physics" },
  { name: "Electronics" },
  { name: "AI" },
  { name: "Cybersecurity" },
  { name: "OOP" },
  { name: "Mathematics" },
  { name: "Statistics" }
];

async function seed(client) {
  console.log("Seeding tags...");
  const seededTags = [];
  for (const tag of tags) {
    const res = await client.query(`
      INSERT INTO public.tags (name, created_at, updated_at, usage_count)
      VALUES ($1, NOW(), NOW(), 0)
      ON CONFLICT (name) DO UPDATE
      SET updated_at = NOW()
      RETURNING id, name
    `, [tag.name]);
    seededTags.push(res.rows[0]);
  }
  console.log(`Seeded ${seededTags.length} tags.`);
  return seededTags;
}

module.exports = { seed };
