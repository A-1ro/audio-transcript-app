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
builder.Services.AddSingleton<CosmosClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();
    var connectionString = configuration["CosmosDb:ConnectionString"];
    
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        // Only allow fallback to emulator in Development environment
        if (hostEnvironment.IsDevelopment())
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(
                "CosmosDb:ConnectionString is not configured. Using local Cosmos DB emulator. " +
                "This is only allowed in Development environment.");
            
            // Use local Cosmos DB emulator endpoint (key should be configured in local.settings.json)
            var emulatorEndpoint = configuration["CosmosDb:EmulatorEndpoint"] ?? "https://localhost:8081";
            var emulatorKey = configuration["CosmosDb:EmulatorKey"];
            
            if (string.IsNullOrWhiteSpace(emulatorKey))
            {
                throw new InvalidOperationException(
                    "CosmosDb:EmulatorKey must be configured in local.settings.json for local development. " +
                    "Never hardcode credentials in source code.");
            }
            
            return new CosmosClient(emulatorEndpoint, emulatorKey);
        }
        
        throw new InvalidOperationException(
            "CosmosDb:ConnectionString is required but not configured. " +
            "Please set the connection string in application settings.");
    }
    
    return new CosmosClient(connectionString);
});

// Job Repository の登録
builder.Services.AddSingleton<IJobRepository, CosmosDbJobRepository>();

// Transcription Repository の登録
builder.Services.AddSingleton<ITranscriptionRepository, CosmosDbTranscriptionRepository>();

builder.Build().Run();
