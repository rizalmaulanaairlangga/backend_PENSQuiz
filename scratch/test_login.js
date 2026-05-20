async function run() {
  const url = "https://itiaxchjjeimdcxpazwa.supabase.co/auth/v1/token?grant_type=password";
  const anonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Iml0aWF4Y2hqamVpbWRjeHBhendhIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Nzg0NzA0MDgsImV4cCI6MjA5NDA0NjQwOH0.7lHEjstBZVpzoStKb6jFKPLGsKTqIedsAMG-evE4aIA";
  
  try {
    const res = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "apikey": anonKey,
        "Authorization": `Bearer ${anonKey}`
      },
      body: JSON.stringify({
        email: "aisha@student.pens.ac.id",
        password: "PENSQuiz123!"
      })
    });
    
    console.log("Status:", res.status);
    console.log("Headers:", Object.fromEntries(res.headers.entries()));
    const text = await res.text();
    console.log("Body:", text);
  } catch (err) {
    console.error(err);
  }
}

run();
