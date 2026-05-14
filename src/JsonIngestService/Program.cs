using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using JsonIngestService.DataAccess;
using JsonIngestService.Pipeline;
using JsonIngestService.Worker;

// Bootstrap logger for startup errors; reconfigured from appsettings by UseSerilog() below.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("JsonIngestService starting up");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((ctx, _, loggerConfig) =>
            loggerConfig.ReadFrom.Configuration(ctx.Configuration))
        .ConfigureServices((ctx, services) =>
        {
            var config = ctx.Configuration;

            // Options
            services.Configure<IngestOptions>(config.GetSection("Ingest"));

            // Connection factory (connection string can come from env var in K8s secret)
            var connString = config.GetConnectionString("CdmDatabase")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:CdmDatabase is not configured.");
            services.AddSingleton<IDbConnectionFactory>(_ => new DbConnectionFactory(connString));

            // Bulk copy settings
            var bulkOpts = config.GetSection("Ingest:BulkCopy").Get<BulkCopyOptions>()
                           ?? new BulkCopyOptions();
            services.AddSingleton(sp =>
                new BulkCopyHelper(
                    bulkOpts.BatchSize,
                    bulkOpts.TimeoutSeconds,
                    sp.GetRequiredService<ILogger<BulkCopyHelper>>()));

            // Pipeline
            services.AddSingleton<JsonFileInserter>();
            services.AddSingleton<IdentifierProcessor>();
            services.AddSingleton<LocationProcessor>();
            services.AddSingleton<EventPathProcessor>();
            services.AddSingleton<SubscriberPathProcessor>();
            services.AddSingleton<IngestionOrchestrator>();

            // File drop source (swap for a Service Bus consumer as needed)
            services.AddSingleton<IIngestionSource, FileDropIngestionSource>();

            // Hosted worker
            services.AddHostedService<IngestWorker>();
        })
        .Build();

    await host.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "JsonIngestService terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
