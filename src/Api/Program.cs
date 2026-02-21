using System.Net.Http.Headers;
using Api.Configuration;
using Api.HostedServices;
using Api.Middleware;
using Infrastructure.Data;
using Infrastructure.Entities;
using Infrastructure.Integrations.Altegio;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Serilog;
using Shared.Abstractions;
using Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.ConfigureSerilog();

// Add services
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetails = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation error",
                Instance = context.HttpContext.Request.Path
            };
            return new BadRequestObjectResult(problemDetails);
        };
    });

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.CommandTimeout(30);
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    });
});

// Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

// Swagger
builder.Services.AddSwaggerConfiguration();

// CORS
builder.Services.AddCors();

// Altegio integration
builder.AddAltegioConfiguration();

builder.Services.AddHttpClient<IAltegioService, AltegioService>((serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AltegioSettings>>().Value;

    client.BaseAddress = new Uri(settings.BaseUrl);
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", $"{settings.BearerToken},{settings.UserToken}");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api.v2+json"));
});

// Application services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAltegioTransactionIngestionService, AltegioTransactionIngestionService>();
builder.Services.AddHostedService<AltegioTransactionsSyncHostedService>();
builder.Services.AddSingleton<ITimeProvider, SystemTimeProvider>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var app = builder.Build();

// Middleware
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors(o => o
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithOrigins("http://localhost:4200", "https://localhost:4200"));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kudzierki API v1"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Database initialization with retry policy
var retryPolicy = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 10,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential,
        OnRetry = args =>
        {
            Log.Warning("Database initialization attempt {AttemptNumber} failed. Retrying in {RetryDelay}...",
                args.AttemptNumber, args.RetryDelay);
            return ValueTask.CompletedTask;
        }
    })
    .Build();

await retryPolicy.ExecuteAsync(async cancellationToken =>
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var timeProvider = scope.ServiceProvider.GetRequiredService<ITimeProvider>();

    Log.Information("Applying database migrations...");
    await dbContext.Database.MigrateAsync(cancellationToken);

    Log.Information("Seeding database...");
    await DbInitializer.SeedAsync(dbContext, timeProvider);

    Log.Information("Database initialization completed successfully");
}, CancellationToken.None);

Log.Information("Starting Kudzierki API...");
app.Run();

public partial class Program { }
