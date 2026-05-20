const { getClient } = require('../seeders/db');

async function run() {
  const client = await getClient();
  try {
    console.log("Updating users in auth.users...");
    const updateUsersRes = await client.query(`
      UPDATE auth.users
      SET 
        confirmation_token = COALESCE(confirmation_token, ''),
        recovery_token = COALESCE(recovery_token, ''),
        email_change_token_new = COALESCE(email_change_token_new, ''),
        email_change = COALESCE(email_change, ''),
        email_change_token_current = COALESCE(email_change_token_current, ''),
        reauthentication_token = COALESCE(reauthentication_token, ''),
        phone_change = COALESCE(phone_change, ''),
        phone_change_token = COALESCE(phone_change_token, '')
    `);
    console.log(`Updated ${updateUsersRes.rowCount} users in auth.users.`);

    console.log("Updating identities in auth.identities...");
    const updateIdentitiesRes = await client.query(`
      UPDATE auth.identities i
      SET email = u.email
      FROM auth.users u
      WHERE i.user_id = u.id AND (i.email IS NULL OR i.email = '')
    `);
    console.log(`Updated ${updateIdentitiesRes.rowCount} identities in auth.identities.`);

  } catch (err) {
    console.error(err);
  } finally {
    await client.end();
  }
}

run();
