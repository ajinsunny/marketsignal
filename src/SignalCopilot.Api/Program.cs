using System.Text;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SignalCopilot.Api.Data;
using SignalCopilot.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Get database connection string (supports both local and production configs)
// Try multiple sources in order of precedence
var connectionString =
    builder.Configuration["DATABASE_URL"] ??
    Environment.GetEnvironmentVariable("DATABASE_URL") ??
    builder.Configuration.GetConnectionString("DefaultConnection");

// Validate and provide detailed error if not found
if (string.IsNullOrWhiteSpace(connectionString))
{
    var envVars = Environment.GetEnvironmentVariables();
    Console.WriteLine("=== DEBUGGING: Environment variables ===");
    foreach (var key in envVars.Keys)
    {
        if (key.ToString().Contains("DATABASE", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{key}: {envVars[key]}");
        }
    }
    throw new InvalidOperationException("Database connection string not configured. Set DATABASE_URL environment variable or ConnectionStrings:DefaultConnection in appsettings.json");
}

Console.WriteLine($"=== CONNECTION STRING DEBUG ===");
Console.WriteLine($"Length: {connectionString.Length}");
Console.WriteLine($"First 50 chars: {connectionString.Substring(0, Math.Min(50, connectionString.Length))}");
Console.WriteLine($"Starts with 'postgresql://': {connectionString.StartsWith("postgresql://")}");
Console.WriteLine($"================================");

// Add database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

// Add Hangfire (using same connection string)
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(connectionString));
builder.Services.AddHangfireServer();

// Add application services

// News providers (multi-source architecture)
builder.Services.AddHttpClient<SignalCopilot.Api.Services.News.NewsApiProvider>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "SignalCopilot/1.0");
});
builder.Services.AddScoped<SignalCopilot.Api.Services.News.INewsProvider, SignalCopilot.Api.Services.News.NewsApiProvider>(sp =>
    sp.GetRequiredService<SignalCopilot.Api.Services.News.NewsApiProvider>());

builder.Services.AddHttpClient<SignalCopilot.Api.Services.News.FinnhubProvider>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "SignalCopilot/1.0");
});
builder.Services.AddScoped<SignalCopilot.Api.Services.News.INewsProvider, SignalCopilot.Api.Services.News.FinnhubProvider>(sp =>
    sp.GetRequiredService<SignalCopilot.Api.Services.News.FinnhubProvider>());

// News aggregation service
builder.Services.AddScoped<SignalCopilot.Api.Services.News.NewsAggregationService>();

// Legacy news service (now uses aggregation)
builder.Services.AddScoped<SignalCopilot.Api.Services.INewsService, SignalCopilot.Api.Services.NewsApiService>();

builder.Services.AddHttpClient<SignalCopilot.Api.Services.IImageProcessor, SignalCopilot.Api.Services.ClaudeImageProcessor>();

// Add new analytics and personalization services
builder.Services.AddScoped<SignalCopilot.Api.Services.IConsensusCalculator, SignalCopilot.Api.Services.ConsensusCalculator>();
builder.Services.AddScoped<SignalCopilot.Api.Services.IPortfolioAnalytics, SignalCopilot.Api.Services.PortfolioAnalytics>();

builder.Services.AddScoped<SignalCopilot.Api.Services.ISentimentAnalyzer, SignalCopilot.Api.Services.SentimentAnalyzer>();
builder.Services.AddScoped<SignalCopilot.Api.Services.IImpactCalculator, SignalCopilot.Api.Services.ImpactCalculator>();
builder.Services.AddScoped<SignalCopilot.Api.Services.IAlertService, SignalCopilot.Api.Services.AlertService>();
builder.Services.AddScoped<SignalCopilot.Api.Services.IPortfolioAnalyzer, SignalCopilot.Api.Services.PortfolioAnalyzer>();
builder.Services.AddScoped<SignalCopilot.Api.Services.BackgroundJobsService>();

// PHASE 4A: Historical analogs service for evidence-based recommendations
builder.Services.AddScoped<SignalCopilot.Api.Services.IHistoricalAnalogService, SignalCopilot.Api.Services.HistoricalAnalogService>();

// Add controllers with JSON configuration for enums
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration["AllowedOrigins"];
        if (!string.IsNullOrEmpty(allowedOrigins))
        {
            // Production: Use specific origins
            var origins = allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(o => o.Trim())
                .ToArray();
            policy.WithOrigins(origins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            // Development: Allow any origin
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Signal Copilot API",
        Version = "v1",
        Description = "API for Signal Copilot - Converting financial news into personalized impact alerts"
    });

    // Add JWT authentication to Swagger
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// Add Hangfire Dashboard
var hangfireEnabled = builder.Configuration.GetValue<bool>("Hangfire:DashboardEnabled", true);
var hangfireRequireAuth = builder.Configuration.GetValue<bool>("Hangfire:RequireAuthentication", false);

if (hangfireEnabled)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = hangfireRequireAuth
            ? new[] { new HangfireAuthorizationFilter(requireAuthentication: true) }
            : new[] { new HangfireAuthorizationFilter(requireAuthentication: false) }
    });
}

app.MapControllers();

// Configure background jobs
SignalCopilot.Api.Services.BackgroundJobsService.ConfigureRecurringJobs();

app.Run();

// Hangfire authorization filter
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly bool _requireAuthentication;

    public HangfireAuthorizationFilter(bool requireAuthentication = false)
    {
        _requireAuthentication = requireAuthentication;
    }

    public bool Authorize(DashboardContext context)
    {
        if (!_requireAuthentication)
        {
            // Development mode: Allow all access
            return true;
        }

        // Production mode: Require authentication
        var httpContext = context.GetHttpContext();

        // Check if user is authenticated via JWT
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            return true;
        }

        // Optionally: Check for a specific Hangfire dashboard key in headers
        var dashboardKey = httpContext.Request.Headers["X-Hangfire-Dashboard-Key"].FirstOrDefault();
        var configuredKey = httpContext.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetValue<string>("Hangfire:DashboardKey");

        if (!string.IsNullOrEmpty(dashboardKey) &&
            !string.IsNullOrEmpty(configuredKey) &&
            dashboardKey == configuredKey)
        {
            return true;
        }

        // Deny access
        return false;
    }
}
