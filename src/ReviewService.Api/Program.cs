using Microsoft.OpenApi.Models;
using ReviewService.Application.Interfaces;
using ReviewService.Application.Services;
using ReviewService.Domain.Repositories;
using ReviewService.Infrastructure.Clients;
using ReviewService.Infrastructure.Repositories;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();







//Register Dapper Repos and Application Services
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IReviewService, ReviewService.Application.Services.ReviewService>();

//Register HttpClient for User,Business and Location Services
builder.Services.AddHttpClient<IBusinessServiceClient, BusinessServiceClient>(client =>
{
    var baseUrl = builder.Configuration["Services:BusinessServiceBaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Missing configuration: Services:BusinessServiceBaseUrl");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");

});


builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
{
    var baseUrl = builder.Configuration["Services:UserServiceBaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Missing configuration: Services:UserServiceBaseUrl");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});


builder.Services.AddHttpClient<ILocationServiceClient, LocationServiceClient>(client =>
{
    var baseUrl = builder.Configuration["Services:LocationServiceBaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Missing configuration: Services:LocationServiceBaseUrl");

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
        Description = "Review Service Built eith .Net 9"
    });
});

var app = builder.Build();


// ✅ Dapper naming convention fix
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

// 🔒 HTTPS
app.UseHttpsRedirection();



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = "swagger";
    });
}


app.MapControllers();



app.Run();
