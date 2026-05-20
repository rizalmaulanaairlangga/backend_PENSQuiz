const fs = require('fs');
const path = require('path');
const { Client } = require('pg');

process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

function loadEnv() {
  const envPath = path.join(__dirname, '../.env');
  if (fs.existsSync(envPath)) {
    const content = fs.readFileSync(envPath, 'utf8');
    const lines = content.split(/\r?\n/);
    for (const line of lines) {
      const trimmed = line.trim();
      if (!trimmed || trimmed.startsWith('#')) continue;
      const eqIdx = trimmed.indexOf('=');
      if (eqIdx !== -1) {
        const key = trimmed.substring(0, eqIdx).trim();
        const value = trimmed.substring(eqIdx + 1).trim();
        process.env[key] = value;
      }
    }
  }
}

function parseAdoNetConnectionString(connStr) {
  const parts = connStr.split(';');
  const config = {};
  for (const part of parts) {
    const trimmedPart = part.trim();
    if (!trimmedPart) continue;
    const eqIdx = trimmedPart.indexOf('=');
    if (eqIdx !== -1) {
      const key = trimmedPart.substring(0, eqIdx).trim().toLowerCase();
      const val = trimmedPart.substring(eqIdx + 1).trim();
      config[key] = val;
    }
  }
  return config;
}

async function getClient() {
  loadEnv();

  const connectionString = process.env.SUPABASE_CONNECTION_STRING || process.env.DATABASE_URL;
  let clientConfig = {};

  if (connectionString) {
    // If it's an ADO.NET connection string (key=value format)
    if (connectionString.includes('Host=') || connectionString.includes('Server=')) {
      const parsed = parseAdoNetConnectionString(connectionString);
      const host = parsed['host'] || parsed['server'];
      const database = parsed['database'] || parsed['db'];
      const user = parsed['username'] || parsed['user id'] || parsed['user'];
      const password = parsed['password'];
      const port = parseInt(parsed['port']) || 5432;
      
      let ssl = false;
      if (parsed['ssl mode']?.toLowerCase() === 'require' || parsed['ssl']?.toLowerCase() === 'true') {
        ssl = { rejectUnauthorized: false };
      }
      
      clientConfig = {
        host,
        database,
        user,
        password,
        port,
        ssl
      };
    } else {
      // Postgres URI format
      clientConfig = {
        connectionString: connectionString,
        ssl: connectionString.includes('sslmode=require') || connectionString.includes('ssl=true')
          ? { rejectUnauthorized: false }
          : false
      };
    }
  } else {
    console.warn("WARNING: No connection string found in .env. Using fallback local connection.");
    clientConfig = {
      connectionString: "postgres://postgres:postgres@localhost:5432/pensquiz"
    };
  }

  const client = new Client(clientConfig);
  await client.connect();
  return client;
}

module.exports = {
  getClient
};
