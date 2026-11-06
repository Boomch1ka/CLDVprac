using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        // Application Insights for the isolated worker (monitoring task f).
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Let Information-level logs flow to App Insights (the default filter only keeps Warning+).
        services.Configure<LoggerFilterOptions>(options =>
        {
            var rule = options.Rules.FirstOrDefault(r =>
                r.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
            if (rule is not null) options.Rules.Remove(rule);
        });

        // Table client used by the HTTP GET function for flexible telemetry queries.
        services.AddSingleton(_ => new TableServiceClient(context.Configuration["AzureWebJobsStorage"]));
    })
    .Build();

host.Run();
