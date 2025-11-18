using Microsoft.OpenApi.Models;
using ReviewService.Application.Interfaces;
using ReviewService.Application.Services;
using ReviewService.Domain.Repositories;
using ReviewService.Infrastructure.Clients;
using ReviewService.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddDapr(); //Enable Dapr integration

//Add Dapr client
builder.Services.AddDaprClient();

// Register Dapper Repos and Application Services
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IReviewService, ReviewService.Application.Services.ReviewService>();

//NEW: Register validation service
builder.Services.AddScoped<IReviewValidationService, ReviewValidationService>();

// Register HttpClient for Business Service
builder.Services.AddHttpClient<IBusinessServiceClient, BusinessServiceClient>(client =>
{
    var baseUrl = builder.Configuration["Services:BusinessServiceBaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Missing configuration: Services:BusinessServiceBaseUrl");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Register HttpClient for User Service
builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
{
    var baseUrl = builder.Configuration["Services:UserServiceBaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Missing configuration: Services:UserServiceBaseUrl");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

//NEW: Register HttpClient for Compliance Service
builder.Services.AddHttpClient<IComplianceServiceClient, ComplianceServiceClient>(client =>
{
    var baseUrl = builder.Configuration["Services:ComplianceServiceBaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Missing configuration: Services:ComplianceServiceBaseUrl");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30); // Longer timeout for validation
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

//NEW: Register HttpClient for Notification Service
builder.Services.AddHttpClient<INotificationServiceClient, NotificationServiceClient>(client =>
{
    var baseUrl = builder.Configuration["Services:NotificationServiceBaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Missing configuration: Services:NotificationServiceBaseUrl");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "REVIEW_SERVICE",
        Version = "v1",
        Description = "Review Service Built with .Net 9"
    });
});

var app = builder.Build();

//Dapper naming convention fix
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// 🔒 HTTPS
app.UseHttpsRedirection();

//Enable Dapr pub/sub
app.UseCloudEvents(); // Required for Dapr pub/sub
app.MapSubscribeHandler(); // Automatically discover [Topic] attributes

// 1️⃣ Swagger setup
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Review Service API v1");
    options.RoutePrefix = ""; // load Swagger at root
});

app.MapControllers();

app.Run();