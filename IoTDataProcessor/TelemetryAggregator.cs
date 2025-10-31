using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Google.Protobuf;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace IoTDataProcessor
{
    public class TelemetryAggregator
    {
        private readonly ILogger<TelemetryAggregator> _logger;

        public TelemetryAggregator(ILogger<TelemetryAggregator> logger)
        {
            _logger = logger;
        }

        [Function("TelemetryAggregator")]
        public async Task Run(
            [ServiceBusTrigger("telemetry-topic", "aggregation-sub", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            _logger.LogInformation("TelemetryAggregator function triggered");

            try
            {
                // Deserialize Protobuf message
                var telemetry = Iotdataprocessor.Telemetry.Parser.ParseFrom(message.Body);

                _logger.LogInformation($"Processing telemetry: Sensor={telemetry.SensorId}, Value={telemetry.Value}, Unit={telemetry.Unit}");

                // Create aggregate data (in a real scenario, you'd batch multiple messages)
                var aggregate = new Iotdataprocessor.TelemetryAggregate
                {
                    SensorId = telemetry.SensorId,
                    WindowStart = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds(),
                    WindowEnd = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    AvgValue = telemetry.Value,
                    MinValue = telemetry.Value,
                    MaxValue = telemetry.Value,
                    Count = 1,
                    Unit = telemetry.Unit
                };

                // Serialize aggregate to JSON for storage
                var jsonData = JsonSerializer.Serialize(new
                {
                    sensorId = aggregate.SensorId,
                    windowStart = DateTimeOffset.FromUnixTimeMilliseconds(aggregate.WindowStart),
                    windowEnd = DateTimeOffset.FromUnixTimeMilliseconds(aggregate.WindowEnd),
                    avgValue = aggregate.AvgValue,
                    minValue = aggregate.MinValue,
                    maxValue = aggregate.MaxValue,
                    count = aggregate.Count,
                    unit = aggregate.Unit,
                    processedAt = DateTimeOffset.UtcNow
                });

                // Store in Blob Storage
                var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                var containerClient = blobServiceClient.GetBlobContainerClient("processed-data");

                // Create container if it doesn't exist
                await containerClient.CreateIfNotExistsAsync();

                // Create blob name with date hierarchy
                var now = DateTimeOffset.UtcNow;
                var blobName = $"{now:yyyy/MM/dd/HH/mm}-{telemetry.SensorId}-{Guid.NewGuid()}.json";

                var blobClient = containerClient.GetBlobClient(blobName);
                await blobClient.UploadAsync(BinaryData.FromString(jsonData), overwrite: true);

                _logger.LogInformation($"Stored aggregated data in blob: {blobName}");

                // Complete the message
                await messageActions.CompleteMessageAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing telemetry message");

                // Dead-letter the message for investigation
                await messageActions.DeadLetterMessageAsync(message, deadLetterReason: ex.Message);
            }
        }

        [Function("TestHttp")]
        public async Task<HttpResponseData> TestHttp([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("TestHttp function triggered");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            await response.WriteStringAsync("IoT Data Processor is running!");
            return response;
        }
    }
}