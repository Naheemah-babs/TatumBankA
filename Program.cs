
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using TatumWelcome2.BLL;
using TatumWelcome2.Database;
using TatumWelcome2.Interfaces;
using TatumWelcome2.Repositories;
using TatumWelcome2.Services;

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

    // Security requirements will be applied to
    // the appropriate controller actions.
});

// MySQL database connection.
builder.Services.AddSingleton<DbConnectionFactory>();

// Account repository using Dapper.
builder.Services.AddSingleton<
    IAccountRepository,
    AccountRepository>();

// Bank-to-bank HTTP communication.
builder.Services.AddHttpClient<
    IBankApiClient,
    BankApiClient>();

// Business logic services.
builder.Services.AddSingleton<InterbankTransferService>();
builder.Services.AddSingleton<InterbankOutgoingTransferService>();
builder.Services.AddSingleton<AdminService>();
builder.Services.AddSingleton<BillPaymentService>();
builder.Services.AddSingleton<OutgoingInterbankTransferRepository>();
builder.Services.AddSingleton<IncomingInterbankTransferRepository>();
builder.Services.AddSingleton<BillPaymentRepository>();


// Automatic interbank reconciliation.
builder.Services.AddHostedService<
    InterbankReconciliationService>();

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

// Test MySQL connection.
try
{
    var connectionFactory =
        app.Services.GetRequiredService<DbConnectionFactory>();

    using var connection =
        connectionFactory.CreateConnection();

    connection.Open();

    int result =
        connection.ExecuteScalar<int>("SELECT 1");

    app.Logger.LogInformation(
        result == 1
            ? "MySQL connection successful!"
            : "MySQL connection test failed.");
}
catch (Exception ex)
{
    app.Logger.LogError(
        ex,
        "MySQL connection error.");
}

// Map all controller endpoints.
app.MapControllers();

// Start the Web API.
app.Run();
