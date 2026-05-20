using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.IdentityModel.Tokens.Jwt;
using PensQuiz.Api.Data;
using PensQuiz.Api.Services;

static string? FindEnvPath(params string?[] startDirs)
{
    foreach (var startDir in startDirs.Where(d => !string.IsNullOrWhiteSpace(d)))
    {
        var current = Path.GetFullPath(startDir!);
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(current, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(current);
            if (parent is null) break;
            current = parent.FullName;
        }
    }

    return null;
}

var envPath = FindEnvPath(
    Directory.GetCurrentDirectory(),
    AppContext.BaseDirectory,
    Path.GetDirectoryName(Environment.ProcessPath ?? "")
);
if (envPath is not null)
{
    DotNetEnv.Env.Load(envPath);
}

var builder = WebApplication.CreateBuilder(args);

static string? NonEmpty(params string?[] values) =>
    values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IStorageService, StorageService>();

var corsOrigins = NonEmpty(builder.Configuration["Cors:AllowedOrigins"], builder.Configuration["CORS_ALLOWED_ORIGINS"]);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (!string.IsNullOrWhiteSpace(corsOrigins))
        {
            var origins = corsOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Auth");
                logger.LogWarning(context.Exception, "JWT authentication failed");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Auth");
                logger.LogInformation(
                    "JWT challenge: error={Error}, desc={Description}",
                    context.Error,
                    context.ErrorDescription
                );
                return Task.CompletedTask;
            }
        };

        // Supabase Auth issues JWTs signed with the project JWT secret (HS256 by default).
        // Configure via appsettings / env vars.
        var supabaseUrl = NonEmpty(builder.Configuration["Supabase:Url"], builder.Configuration["SUPABASE_URL"]);
        var jwtIssuer = NonEmpty(builder.Configuration["Supabase:JwtIssuer"], builder.Configuration["SUPABASE_JWT_ISSUER"]);
        var jwtAudience = NonEmpty(builder.Configuration["Supabase:JwtAudience"], builder.Configuration["SUPABASE_JWT_AUDIENCE"]);
        var jwtSecret = NonEmpty(builder.Configuration["Supabase:JwtSecret"], builder.Configuration["SUPABASE_JWT_SECRET"]);

        // Support asymmetric signing (RS/ES) by using Supabase OpenID metadata/JWKS when SUPABASE_URL is available.
        if (!string.IsNullOrWhiteSpace(supabaseUrl))
        {
            var authority = supabaseUrl.TrimEnd('/') + "/auth/v1";
            options.Authority = authority;
            options.RequireHttpsMetadata = authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            // If issuer isn't explicitly configured, default to Supabase auth issuer.
            if (string.IsNullOrWhiteSpace(jwtIssuer))
            {
                jwtIssuer = authority;
            }
        }

        static byte[] GetJwtSecretBytes(string secret)
        {
            // Supabase dashboard sometimes shows the JWT secret as base64.
            // Accept both plain text and base64-encoded secrets.
            try
            {
                var bytes = Convert.FromBase64String(secret);
                // Basic sanity: HS256 key should not be tiny; fall back if it looks wrong.
                if (bytes.Length >= 32) return bytes;
            }
            catch
            {
                // ignore and fall back
            }

            return Encoding.UTF8.GetBytes(secret);
        }

        if (!string.IsNullOrWhiteSpace(jwtIssuer))
        {
            options.TokenValidationParameters.ValidIssuer = jwtIssuer;
            options.TokenValidationParameters.ValidateIssuer = true;
        }

        if (!string.IsNullOrWhiteSpace(jwtAudience))
        {
            options.TokenValidationParameters.ValidAudience = jwtAudience;
            options.TokenValidationParameters.ValidateAudience = true;
        }

        // Signing key resolution:
        // - HS* tokens: validate with SUPABASE_JWT_SECRET (if set)
        // - RS*/ES* tokens: validate with JWKS from Authority (if configured)
        SymmetricSecurityKey? hmacKey = null;
        if (!string.IsNullOrWhiteSpace(jwtSecret))
        {
            hmacKey = new SymmetricSecurityKey(GetJwtSecretBytes(jwtSecret));
        }

        options.TokenValidationParameters.IssuerSigningKeyResolver = (rawToken, securityToken, kid, parameters) =>
        {
            if (securityToken is JwtSecurityToken jwt && jwt.Header?.Alg is string alg)
            {
                if (alg.StartsWith("HS", StringComparison.OrdinalIgnoreCase))
                {
                    return hmacKey is null ? Array.Empty<SecurityKey>() : new SecurityKey[] { hmacKey };
                }
            }

            // Fall back to keys from OpenID configuration (Authority/JWKS).
            var config = options.ConfigurationManager is null
                ? null
                : options.ConfigurationManager.GetConfigurationAsync(default).GetAwaiter().GetResult();
            return config?.SigningKeys ?? Array.Empty<SecurityKey>();
        };

        // Enforce signing key validation; resolver will supply appropriate keys.
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;

        options.TokenValidationParameters.NameClaimType = "sub";
        options.TokenValidationParameters.RoleClaimType = "role";
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ValidateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer);
        options.TokenValidationParameters.ValidateAudience = !string.IsNullOrWhiteSpace(jwtAudience);
    });

builder.Services.AddAuthorization();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Avoid noisy dev warning when no HTTPS port is configured.
var httpsPort = builder.Configuration["ASPNETCORE_HTTPS_PORT"];
if (!string.IsNullOrWhiteSpace(httpsPort))
{
    app.UseHttpsRedirection();
}

// Auth diagnostics (do not log full tokens).
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("AuthTrace");

        var auth = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth))
        {
            logger.LogInformation("Request {Method} {Path} Authorization: <missing>", context.Request.Method,
                context.Request.Path);
        }
        else
        {
            var preview = auth.Length <= 24 ? auth : auth.Substring(0, 24) + "...";
            logger.LogInformation("Request {Method} {Path} Authorization: {Preview}", context.Request.Method,
                context.Request.Path, preview);
        }
    }

    await next();
});

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
