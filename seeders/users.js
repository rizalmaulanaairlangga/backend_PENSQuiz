const bcrypt = require('bcryptjs');
const { v4: uuidv4 } = require('uuid');

const PASSWORD_PLAIN = "PENSQuiz123!";

const students = [
  {
    firstName: "Aisha",
    lastName: "Zarrah Amalia",
    username: "aisha",
    email: "aisha@student.pens.ac.id",
    majorName: "Teknik Informatika",
    yearOfEntry: 2024
  },
  {
    firstName: "Nicolaus",
    lastName: "Prima Dharma N.",
    username: "nicolaus",
    email: "nicolaus@student.pens.ac.id",
    majorName: "Teknik Elektronika",
    yearOfEntry: 2024
  },
  {
    firstName: "Rizal",
    lastName: "Maulana Airlangga",
    username: "rizal",
    email: "rizal@student.pens.ac.id",
    majorName: "Teknik Informatika",
    yearOfEntry: 2024
  },
  {
    firstName: "Ahmad",
    lastName: "Rohmat Hisyamuddin",
    username: "ahmad",
    email: "ahmad@student.pens.ac.id",
    majorName: "Teknik Informatika",
    yearOfEntry: 2024
  }
];

async function seed(client) {
  console.log("Seeding student users...");

  // 1. Fetch majors to map IDs
  const majorsRes = await client.query("SELECT id, name FROM public.majors");
  const majorsMap = {};
  majorsRes.rows.forEach(m => {
    majorsMap[m.name] = m.id;
  });

  // 2. Hash the password
  console.log("Hashing password...");
  const salt = await bcrypt.genSalt(10);
  const passwordHash = await bcrypt.hash(PASSWORD_PLAIN, salt);

  // 3. Process each student
  for (const student of students) {
    const majorId = majorsMap[student.majorName];
    if (!majorId) {
      console.error(`Major '${student.majorName}' not found in database! Skipping ${student.email}`);
      continue;
    }

    console.log(`Processing student: ${student.firstName} ${student.lastName} (${student.email})...`);

    // Check if user exists in auth.users
    const userCheck = await client.query("SELECT id FROM auth.users WHERE email = $1", [student.email]);
    let userId;

    const rawUserMetadata = {
      first_name: student.firstName,
      last_name: student.lastName,
      username: student.username
    };

    const rawAppMetadata = {
      provider: "email",
      providers: ["email"]
    };

    if (userCheck.rows.length > 0) {
      // User exists -> UPDATE
      userId = userCheck.rows[0].id;
      
      // Update auth.users
      await client.query(`
        UPDATE auth.users
        SET 
          encrypted_password = $1,
          raw_user_meta_data = $2,
          updated_at = NOW(),
          email_confirmed_at = COALESCE(email_confirmed_at, NOW()),
          confirmation_token = COALESCE(confirmation_token, ''),
          recovery_token = COALESCE(recovery_token, ''),
          email_change_token_new = COALESCE(email_change_token_new, ''),
          email_change = COALESCE(email_change, ''),
          email_change_token_current = COALESCE(email_change_token_current, ''),
          reauthentication_token = COALESCE(reauthentication_token, ''),
          phone_change = COALESCE(phone_change, ''),
          phone_change_token = COALESCE(phone_change_token, '')
        WHERE id = $3
      `, [passwordHash, JSON.stringify(rawUserMetadata), userId]);

    } else {
      // User does not exist -> INSERT
      userId = uuidv4();

      // Insert into auth.users
      await client.query(`
        INSERT INTO auth.users (
          id,
          instance_id,
          email,
          encrypted_password,
          email_confirmed_at,
          raw_app_meta_data,
          raw_user_meta_data,
          created_at,
          updated_at,
          aud,
          role,
          is_anonymous,
          is_sso_user,
          confirmation_token,
          recovery_token,
          email_change_token_new,
          email_change,
          email_change_token_current,
          reauthentication_token,
          phone_change,
          phone_change_token
        ) VALUES (
          $1, '00000000-0000-0000-0000-000000000000', $2, $3, NOW(), $4, $5, NOW(), NOW(), 'authenticated', 'authenticated', false, false,
          '', '', '', '', '', '', '', ''
        )
      `, [userId, student.email, passwordHash, JSON.stringify(rawAppMetadata), JSON.stringify(rawUserMetadata)]);
    }

    // Upsert auth.identities
    const identityData = {
      sub: userId,
      email: student.email,
      email_verified: true,
      phone_verified: false
    };

    const identityCheck = await client.query(
      "SELECT id FROM auth.identities WHERE user_id = $1 AND provider = 'email'",
      [userId]
    );

    if (identityCheck.rows.length > 0) {
      await client.query(`
        UPDATE auth.identities
        SET 
          identity_data = $1,
          updated_at = NOW()
        WHERE user_id = $2 AND provider = 'email'
      `, [JSON.stringify(identityData), userId]);
    } else {
      await client.query(`
        INSERT INTO auth.identities (
          id,
          user_id,
          identity_data,
          provider,
          provider_id,
          created_at,
          updated_at
        ) VALUES (
          $1, $2, $3, 'email', $4, NOW(), NOW()
        )
      `, [uuidv4(), userId, JSON.stringify(identityData), userId]);
    }

    // Upsert public.profiles
    await client.query(`
      INSERT INTO public.profiles (
        id,
        first_name,
        last_name,
        username,
        major_id,
        year_of_entry,
        role,
        created_at,
        updated_at
      ) VALUES (
        $1, $2, $3, $4, $5, $6, 'student', NOW(), NOW()
      )
      ON CONFLICT (id) DO UPDATE
      SET
        first_name = EXCLUDED.first_name,
        last_name = EXCLUDED.last_name,
        username = EXCLUDED.username,
        major_id = EXCLUDED.major_id,
        year_of_entry = EXCLUDED.year_of_entry,
        updated_at = NOW(),
        deleted_at = NULL
    `, [
      userId,
      student.firstName,
      student.lastName,
      student.username,
      majorId,
      student.yearOfEntry
    ]);
  }
  console.log("Student users seeded successfully.");
}

module.exports = { seed };
