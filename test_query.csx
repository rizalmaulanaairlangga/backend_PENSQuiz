#r "nuget: Npgsql, 8.0.3"
#r "nuget: Dapper, 2.1.35"
using System;
using System.Data;
using System.Threading.Tasks;
using Npgsql;
using Dapper;

var connStr = "Host=localhost;Port=5432;Database=pensquiz;Username=postgres;Password=postgres";
using var conn = new NpgsqlConnection(connStr);
conn.Open();
var courses = conn.Query("select id as Id, name as Name, major_id as MajorId from public.courses where @MajorId is null or major_id = @MajorId order by name", new { MajorId = (Guid?)null });
foreach(var c in courses) {
    Console.WriteLine($"Course: {c.name}");
}
Console.WriteLine("Done.");
