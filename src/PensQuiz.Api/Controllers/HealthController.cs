using Microsoft.AspNetCore.Mvc;
using Dapper;
using PensQuiz.Api.Data;

namespace PensQuiz.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(IDbConnectionFactory db, IConfiguration configuration, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", time = DateTimeOffset.UtcNow });

    [HttpGet("config")]
    public IActionResult Config()
    {
        if (!env.IsDevelopment())
        {
            return NotFound();
        }

        static string Mask(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            if (value.Length <= 8) return "***";
            return $"{value[..3]}***{value[^3..]}";
        }

        static string? NonEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var supabaseUrl = NonEmpty(configuration["Supabase:Url"], configuration["SUPABASE_URL"]);
        var supabaseAnon = NonEmpty(
            configuration["Supabase:AnonKey"],
            configuration["SUPABASE_ANON_KEY"],
            configuration["SUPABASE_PUBLISHABLE_KEY"]
        );
        var supabaseConn = NonEmpty(
            configuration.GetConnectionString("SupabasePostgres"),
            configuration["Supabase:ConnectionString"],
            configuration["SUPABASE_CONNECTION_STRING"]
        );

        var processEnv = new
        {
            SUPABASE_URL = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_URL")),
            SUPABASE_ANON_KEY = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY")),
            SUPABASE_PUBLISHABLE_KEY = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_PUBLISHABLE_KEY")),
            SUPABASE_CONNECTION_STRING = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")),
            SUPABASE_JWT_SECRET = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_JWT_SECRET")),
        };

        var configEnv = new
        {
            SUPABASE_URL = configuration["SUPABASE_URL"] ?? "",
            SUPABASE_ANON_KEY = configuration["SUPABASE_ANON_KEY"] ?? "",
            SUPABASE_PUBLISHABLE_KEY = configuration["SUPABASE_PUBLISHABLE_KEY"] ?? "",
            SUPABASE_CONNECTION_STRING = configuration["SUPABASE_CONNECTION_STRING"] ?? "",
            SUPABASE_JWT_SECRET = configuration["SUPABASE_JWT_SECRET"] ?? "",
        };

        var cwd = Directory.GetCurrentDirectory();
        var candidateEnvPaths = new
        {
            currentDir = cwd,
            cwdEnvPath = Path.GetFullPath(Path.Combine(cwd, ".env")),
            contentRoot = env.ContentRootPath,
            backendRootEnvPath = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", "..", ".env"))
        };

        return Ok(new
        {
            environment = env.EnvironmentName,
            envPaths = new
            {
                candidateEnvPaths.currentDir,
                cwdEnvExists = System.IO.File.Exists(candidateEnvPaths.cwdEnvPath),
                candidateEnvPaths.contentRoot,
                backendRootEnvExists = System.IO.File.Exists(candidateEnvPaths.backendRootEnvPath)
            },
            envVarsPresent = processEnv,
            configReads = new
            {
                SUPABASE_URL = Mask(configEnv.SUPABASE_URL),
                SUPABASE_ANON_KEY = Mask(NonEmpty(configEnv.SUPABASE_ANON_KEY, configEnv.SUPABASE_PUBLISHABLE_KEY)),
                SUPABASE_CONNECTION_STRING_PRESENT = !string.IsNullOrWhiteSpace(configEnv.SUPABASE_CONNECTION_STRING),
                SUPABASE_JWT_SECRET_PRESENT = !string.IsNullOrWhiteSpace(configEnv.SUPABASE_JWT_SECRET),
            },
            supabase = new
            {
                url = Mask(supabaseUrl),
                anonKey = Mask(supabaseAnon),
                connectionStringPresent = !string.IsNullOrWhiteSpace(supabaseConn),
            }
        });
    }

    [HttpGet("db")]
    public async Task<IActionResult> Db(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = db.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            var one = await connection.ExecuteScalarAsync<int>(new CommandDefinition("select 1", cancellationToken: cancellationToken));
            return Ok(new { ok = one == 1 });
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message, title: "DB connection failed");
        }
    }
}
