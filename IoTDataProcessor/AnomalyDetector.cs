using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Google.Protobuf;
using System.Text.Json;

namespace IoTDataProcessor
{
    public class AnomalyDetector
    {
        private readonly ILogger<AnomalyDetector> _logger;

        public AnomalyDetector(ILogger<AnomalyDetector> logger)
        {
            _logger = logger;
        }

        [Function(nameof(AnomalyDetector))]
        public async Task Run(
            [ServiceBusTrigger("telemetry-topic", "anomaly-detection-sub", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            _logger.LogInformation("AnomalyDetector function triggered");

            try
            {
                // Deserialize Protobuf message
                var telemetry = Iotdataprocessor.Telemetry.Parser.ParseFrom(message.Body);

                _logger.LogInformation($"Analyzing telemetry for anomalies: Sensor={telemetry.SensorId}, Value={telemetry.Value}");

                // Simple threshold-based anomaly detection
                var isAnomaly = IsAnomalous(telemetry);
                Iotdataprocessor.AnomalyAlert? anomaly = null;

                if (isAnomaly)
                {
                    anomaly = new Iotdataprocessor.AnomalyAlert
                    {
                        SensorId = telemetry.SensorId,
                        Timestamp = telemetry.Timestamp,
                        Value = telemetry.Value,
                        AnomalyType = "threshold_exceeded",
                        Threshold = GetThresholdForSensor(telemetry.SensorId),
                        Severity = GetSeverity(telemetry.Value, GetThresholdForSensor(telemetry.SensorId))
                    };

                    _logger.LogWarning($"Anomaly detected: Sensor={anomaly.SensorId}, Value={anomaly.Value}, Severity={anomaly.Severity}");

                    // Serialize anomaly to JSON for storage
                    var jsonData = JsonSerializer.Serialize(new
                    {
                        sensorId = anomaly.SensorId,
                        timestamp = DateTimeOffset.FromUnixTimeMilliseconds(anomaly.Timestamp),
                        value = anomaly.Value,
                        anomalyType = anomaly.AnomalyType,
                        threshold = anomaly.Threshold,
                        severity = anomaly.Severity,
                        detectedAt = DateTimeOffset.UtcNow
                    });

                    // Store in Blob Storage
                    var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                    var containerClient = blobServiceClient.GetBlobContainerClient("anomalies");

                    // Create container if it doesn't exist
                    await containerClient.CreateIfNotExistsAsync();

                    // Create blob name
                    var now = DateTimeOffset.UtcNow;
                    var blobName = $"{now:yyyy/MM/dd/HH}-{anomaly.SensorId}-{Guid.NewGuid()}.json";

                    var blobClient = containerClient.GetBlobClient(blobName);
                    await blobClient.UploadAsync(BinaryData.FromString(jsonData), overwrite: true);

                    _logger.LogInformation($"Stored anomaly data in blob: {blobName}");
                }

                // Complete the message
                await messageActions.CompleteMessageAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing telemetry message for anomaly detection");

                // Dead-letter the message for investigation
                await messageActions.DeadLetterMessageAsync(message, deadLetterReason: ex.Message);
            }
        }

        private bool IsAnomalous(Iotdataprocessor.Telemetry telemetry)
        {
            var threshold = GetThresholdForSensor(telemetry.SensorId);

            // Simple threshold check - in production, you'd use more sophisticated algorithms
            return Math.Abs(telemetry.Value) > threshold;
        }

        private double GetThresholdForSensor(string sensorId)
        {
            // Determine threshold based on sensor type
            if (sensorId.Contains("temperature"))
                return 100.0; // Celsius
            else if (sensorId.Contains("pressure"))
                return 1500.0; // hPa
            else if (sensorId.Contains("humidity"))
                return 100.0; // Percent
            else if (sensorId.Contains("vibration"))
                return 20.0; // mm/s
            else
                return 1000.0; // Default
        }

        private string GetSeverity(double value, double threshold)
        {
            var ratio = Math.Abs(value) / threshold;

            if (ratio > 2.0)
                return "critical";
            else if (ratio > 1.5)
                return "high";
            else
                return "medium";
        }
    }
}