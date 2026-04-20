using BHXH_Backend.Data;
using BHXH_Backend.Helpers;
using BHXH_Backend.Middlewares;
using BHXH_Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
EnvLoader.LoadFromDefaultLocations(builder.Environment.ContentRootPath);

var connectionString = Environment.GetEnvironmentVariable("DB_URL")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing connection string: ConnectionStrings:DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

var jwtKey = ConfigurationHelper.GetJwtSecret(builder.Configuration)
    ?? throw new InvalidOperationException("Missing JWT secret (JWT_SECRET or JwtSettings:SecretKey).");

if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("JWT secret must be at least 32 characters.");
}

var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5 * 1024 * 1024;
});
builder.Services.AddScoped<SystemLogService>();
builder.Services.AddScoped<BlockchainService>();
builder.Services.AddScoped<SecurityAnalyticsService>();
builder.Services.AddScoped<VietQrService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<OtpService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendOnly", policy =>
    {
        var configuredOrigins = (Environment.GetEnvironmentVariable("CORS_ORIGINS")
            ?? Environment.GetEnvironmentVariable("FRONTEND_URL")
            ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (configuredOrigins.Length == 0)
        {
            configuredOrigins = new[]
            {
                "http://localhost:3000",
                "http://localhost"
            };
        }

        policy.WithOrigins(configuredOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<ApplicationDbContext>();

    try
    {
        logger.LogInformation("Waiting for SQL Server startup...");

        var retries = 10;
        while (retries > 0)
        {
            try
            {
                if (context.Database.CanConnect())
                {
                    logger.LogInformation("Database connected. Running migrations...");
                    context.Database.Migrate();
                    break;
                }
            }
            catch
            {
                retries--;
                if (retries == 0)
                {
                    throw;
                }

                logger.LogWarning("Database connection failed. Retry in 5s... ({Retries} left)", retries);
                Thread.Sleep(5000);
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Cannot connect to database.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();
app.UseMiddleware<BlockedIpMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseCors("FrontendOnly");

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
