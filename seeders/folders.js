const foldersData = [
  { username: "aisha", name: "Informatika Core" },
  { username: "nicolaus", name: "Elektronika Instrumentasi" },
  { username: "rizal", name: "Software Engineering Lab" },
  { username: "ahmad", name: "Algorithms & Structures" }
];

async function seed(client) {
  console.log("Seeding folders for users...");
  
  // Get profiles
  const profilesRes = await client.query("SELECT id, username FROM public.profiles");
  const profilesMap = {};
  profilesRes.rows.forEach(p => {
    profilesMap[p.username] = p.id;
  });

  const seededFolders = [];
  for (const f of foldersData) {
    const userId = profilesMap[f.username];
    if (!userId) {
      console.warn(`Warning: User ${f.username} not found for folder seeding.`);
      continue;
    }

    // Check if folder already exists
    const checkRes = await client.query(`
      SELECT id FROM public.folders 
      WHERE user_id = $1 AND name = $2
    `, [userId, f.name]);

    let folderId;
    if (checkRes.rows.length > 0) {
      folderId = checkRes.rows[0].id;
    } else {
      const res = await client.query(`
        INSERT INTO public.folders (user_id, name, created_at, updated_at)
        VALUES ($1, $2, NOW(), NOW())
        RETURNING id, name
      `, [userId, f.name]);
      folderId = res.rows[0].id;
    }
    seededFolders.push({ id: folderId, username: f.username, name: f.name });
  }
  console.log(`Seeded ${seededFolders.length} folders.`);
  return seededFolders;
}

module.exports = { seed };
