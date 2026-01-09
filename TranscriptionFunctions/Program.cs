using Azure.Data.Tables;
using Microsoft.ApplicationInsights;
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

// Table Storage Client の登録
builder.Services.AddSingleton<TableServiceClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration["TableStorage:ConnectionString"];
    
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "TableStorage:ConnectionString is required but not configured. " +
            "Please set the connection string in application settings. " +
            "For local development, use 'UseDevelopmentStorage=true' for Azurite/Storage Emulator.");
    }
    
    return new TableServiceClient(connectionString);
});

// Job Repository の登録
builder.Services.AddSingleton<IJobRepository, TableStorageJobRepository>();

// Transcription Repository の登録
builder.Services.AddSingleton<ITranscriptionRepository, TableStorageTranscriptionRepository>();

// Telemetry Service の登録
builder.Services.AddSingleton<ITelemetryService, ApplicationInsightsTelemetryService>();

builder.Build().Run();
