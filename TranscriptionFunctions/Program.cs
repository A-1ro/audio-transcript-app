using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TranscriptionFunctions.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// HttpClientの登録
builder.Services.AddHttpClient();

// Cosmos DB Client の登録
builder.Services.AddSingleton(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration["CosmosDb:ConnectionString"];
    
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(
            "CosmosDb:ConnectionString is not configured. Using local emulator connection string. " +
            "This should only be used for local development.");
        
        // Use Cosmos DB emulator connection string for local development
        return new CosmosClient(
            "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
    }
    
    return new CosmosClient(connectionString);
});

// Job Repository の登録
builder.Services.AddSingleton<IJobRepository, CosmosDbJobRepository>();

builder.Build().Run();
