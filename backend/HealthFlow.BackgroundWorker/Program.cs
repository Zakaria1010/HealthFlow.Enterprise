using HealthFlow.BackgroundWorker;
using HealthFlow.BackgroundWorker.Services;
using HealthFlow.Services.Patients.Services;
using HealthFlow.Infrastructure;
using HealthFlow.Shared.Messaging;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // 🔌 Add Infrastructure dependencies (RabbitMQ + Cosmos)
        services.AddInfrastructure(configuration);

        // 🧩 Register background processing
        services.AddSingleton<PatientProcessingChannel>();
        services.AddHostedService<PatientProcessingService>();
        
        // HttpClient for calling Analytics API
        services.AddHttpClient<IAnalyticsService, AnalyticsService>(client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["AnalyticsService:BaseUrl"]!);
        });
        services.AddHostedService<Worker>();
    })
    .Build();


await host.RunAsync();

