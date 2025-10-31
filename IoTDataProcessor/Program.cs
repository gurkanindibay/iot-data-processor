using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using IoTDataProcessor.Logging;
using Microsoft.Extensions.Azure;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddAzureClients(builder =>
        {
            builder.AddServiceBusClient(Environment.GetEnvironmentVariable("ServiceBusConnection"));
        });
    })
    .ConfigureLogging(logging =>
    {
        logging.AddBlobLogging(options =>
        {
            options.ConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage") ?? "";
            options.ContainerName = "application-logs";
            options.MaxFileSizeMB = 100;
            options.RetentionDays = 30;
            options.MinimumLogLevel = LogLevel.Information;
        });
    })
    .Build();

host.Run();