const { Client } = require('pg');

async function run() {
  const client = new Client({
    connectionString: "postgres://postgres:postgres@localhost:5432/pensquiz"
  });
  await client.connect();
  const res = await client.query('SELECT id, name, major_id FROM public.courses');
  console.log("DB courses:");
  console.dir(res.rows, { depth: null });
  await client.end();
}

run().catch(console.error);
