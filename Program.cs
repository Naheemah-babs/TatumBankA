using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using TatumWelcome2.API.Security;
using TatumWelcome2.BLL;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.BLL.Options;
using TatumWelcome2.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Bank A API address.
builder.WebHost.UseUrls("https://localhost:5000");

// Register controller-based Web API services.
builder.Services.AddControllers();

// Read JWT settings from User Secrets.
string jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT signing key is missing.");

string jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "JWT issuer is missing.");

string jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "JWT audience is missing.");

// Configure JWT authentication.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();

// Configure rate limiting.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    // Limit login attempts per IP address.
    options.AddPolicy("login", context =>
    {
        string ip = context.Connection.RemoteIpAddress?
            .ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    // Limit PIN-protected transactions per customer.
    options.AddPolicy("pin-transactions", context =>
    {
        string? accountNumber = context.User.Claims
            .FirstOrDefault(claim =>
                claim.Type == ClaimTypes.NameIdentifier ||
                claim.Type == "sub")
            ?.Value;

        string partitionKey = accountNumber
            ?? context.Connection.RemoteIpAddress?
                .ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

// Configure Swagger for controller-based APIs.
builder.Services.AddSwaggerGen(options =>
{
    // Customer JWT authentication.
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token."
        });

    // Admin API key authentication.
    options.AddSecurityDefinition(
        "AdminKey",
        new OpenApiSecurityScheme
        {
            Name = "X-Admin-Key",
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Description = "Enter your admin API key."
        });

    // Without a requirement Swagger UI shows the Authorize button but
    // never attaches the token, so [Authorize] endpoints return 401.
    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});

// Repositories, MySQL connection factory and bank-to-bank HTTP client.
builder.Services.AddTatumInfrastructure();

// Business services (scoped) and stateless helpers.
builder.Services.AddTatumBusinessLogic();

// Partner bank for outgoing interbank transfers (Banks:BankB:Url).
builder.Services.Configure<InterBankOptions>(
    builder.Configuration.GetSection(InterBankOptions.SectionName));

// Customer JWT issuing (same Jwt section the bearer validation above uses).
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// Interbank reconciliation runs in the
// TatumWelcome2.TransactionReconciliation.Processor service.

// Reduce unnecessary logging.
builder.Logging.AddFilter(
    "System.Net.Http.HttpClient",
    LogLevel.Warning);

builder.Logging.AddFilter(
    "Microsoft.AspNetCore",
    LogLevel.Warning);

var app = builder.Build();

// Enable Swagger during development.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Authentication before authorization.
app.UseAuthentication();

app.UseRateLimiter();

app.UseAuthorization();

// Convert any legacy plain-text PINs to PBKDF2 hashes (idempotent).
try
{
    using IServiceScope scope = app.Services.CreateScope();

    int migrated = scope.ServiceProvider
        .GetRequiredService<IPinMigrationService>()
        .HashLegacyPins();

    if (migrated > 0)
    {
        app.Logger.LogWarning(
            "Hashed {Count} legacy plain-text PIN(s).",
            migrated);
    }
}
catch (Exception ex)
{
    app.Logger.LogError(
        ex,
        "Legacy PIN migration failed.");
}

// Map all controller endpoints.
app.MapControllers();

// Start the Web API.
app.Run();

