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

namespace IoTDataProcessor
{
    /// <summary>
    /// TelemetryAggregator with proper batch aggregation logic
    /// Aggregates telemetry data over 5-minute windows with statistical calculations
    /// </summary>
    public class TelemetryAggregatorFixed
    {
        private readonly ILogger<TelemetryAggregatorFixed> _logger;
        private static readonly Dictionary<string, List<TelemetryDataPoint>> _aggregationBuffer = new();
        private static readonly object _lockObject = new();
        private static DateTimeOffset _lastFlushTime = DateTimeOffset.UtcNow;

        public TelemetryAggregatorFixed(ILogger<TelemetryAggregatorFixed> logger)
        {
            _logger = logger;
        }

        [Function("TelemetryAggregatorFixed")]
        public async Task Run(
            [ServiceBusTrigger("telemetry-topic", "aggregation-sub", Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            _logger.LogInformation("TelemetryAggregatorFixed function triggered");

            try
            {
                // Deserialize Protobuf message
                var telemetry = Iotdataprocessor.Telemetry.Parser.ParseFrom(message.Body);

                var dataPoint = new TelemetryDataPoint
                {
                    SensorId = telemetry.SensorId,
                    Value = telemetry.Value,
                    Unit = telemetry.Unit,
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(telemetry.Timestamp)
                };

                _logger.LogInformation($"Received telemetry: Sensor={telemetry.SensorId}, Value={telemetry.Value}, Unit={telemetry.Unit}");

                // Add to aggregation buffer
                lock (_lockObject)
                {
                    var windowKey = GetWindowKey(dataPoint.SensorId, dataPoint.Timestamp);
                    
                    if (!_aggregationBuffer.ContainsKey(windowKey))
                    {
                        _aggregationBuffer[windowKey] = new List<TelemetryDataPoint>();
                    }
                    
                    _aggregationBuffer[windowKey].Add(dataPoint);
                    
                    _logger.LogInformation($"Added to buffer. Window={windowKey}, BufferSize={_aggregationBuffer[windowKey].Count}");
                }

                // Check if we should flush (every 5 minutes or when buffer is large)
                var shouldFlush = false;
                lock (_lockObject)
                {
                    var timeSinceFlush = DateTimeOffset.UtcNow - _lastFlushTime;
                    var totalBufferSize = _aggregationBuffer.Values.Sum(v => v.Count);
                    
                    shouldFlush = timeSinceFlush.TotalMinutes >= 5 || totalBufferSize >= 100;
                }

                if (shouldFlush)
                {
                    await FlushAggregations();
                }

                // Complete the message
                await messageActions.CompleteMessageAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing telemetry message");
                await messageActions.DeadLetterMessageAsync(message, deadLetterReason: ex.Message);
            }
        }

        private async Task FlushAggregations()
        {
            Dictionary<string, List<TelemetryDataPoint>> dataToProcess;
            
            lock (_lockObject)
            {
                dataToProcess = new Dictionary<string, List<TelemetryDataPoint>>(_aggregationBuffer);
                _aggregationBuffer.Clear();
                _lastFlushTime = DateTimeOffset.UtcNow;
            }

            if (!dataToProcess.Any())
            {
                return;
            }

            _logger.LogInformation($"Flushing aggregations for {dataToProcess.Count} windows");

            try
            {
                var blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                var containerClient = blobServiceClient.GetBlobContainerClient("processed-data");
                await containerClient.CreateIfNotExistsAsync();

                foreach (var kvp in dataToProcess)
                {
                    var windowKey = kvp.Key;
                    var dataPoints = kvp.Value;

                    if (!dataPoints.Any())
                        continue;

                    // Parse window key: sensorId_yyyyMMddHHmm
                    var parts = windowKey.Split('_');
                    var sensorId = parts[0];
                    var windowStartStr = parts[1];

                    // Calculate aggregations
                    var values = dataPoints.Select(dp => dp.Value).ToList();
                    var aggregate = new TelemetryAggregateData
                    {
                        SensorId = sensorId,
                        WindowStart = dataPoints.Min(dp => dp.Timestamp),
                        WindowEnd = dataPoints.Max(dp => dp.Timestamp),
                        AvgValue = values.Average(),
                        MinValue = values.Min(),
                        MaxValue = values.Max(),
                        StdDevValue = CalculateStandardDeviation(values),
                        Count = values.Count,
                        Unit = dataPoints.First().Unit,
                        ProcessedAt = DateTimeOffset.UtcNow
                    };

                    // Serialize to JSON
                    var jsonData = JsonSerializer.Serialize(aggregate, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    // Store in Blob Storage with date hierarchy
                    var now = DateTimeOffset.UtcNow;
                    var blobName = $"aggregated/{now:yyyy/MM/dd/HH}/{windowStartStr}-{sensorId}.json";

                    var blobClient = containerClient.GetBlobClient(blobName);
                    await blobClient.UploadAsync(BinaryData.FromString(jsonData), overwrite: true);

                    _logger.LogInformation(
                        $"Aggregated {aggregate.Count} messages for Sensor={aggregate.SensorId}, " +
                        $"Window={aggregate.WindowStart:yyyy-MM-dd HH:mm} to {aggregate.WindowEnd:yyyy-MM-dd HH:mm}, " +
                        $"Avg={aggregate.AvgValue:F2}, Min={aggregate.MinValue:F2}, Max={aggregate.MaxValue:F2}, " +
                        $"StdDev={aggregate.StdDevValue:F2}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing aggregations");
            }
        }

        private static string GetWindowKey(string sensorId, DateTimeOffset timestamp)
        {
            // Round down to 5-minute window
            var minute = (timestamp.Minute / 5) * 5;
            var windowStart = new DateTimeOffset(
                timestamp.Year, timestamp.Month, timestamp.Day,
                timestamp.Hour, minute, 0, timestamp.Offset);
            
            return $"{sensorId}_{windowStart:yyyyMMddHHmm}";
        }

        private static double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count <= 1) return 0;
            
            double avg = values.Average();
            double sumOfSquaresOfDifferences = values.Select(val => (val - avg) * (val - avg)).Sum();
            return Math.Sqrt(sumOfSquaresOfDifferences / values.Count);
        }
    }

    public class TelemetryDataPoint
    {
        public string SensorId { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

    public class TelemetryAggregateData
    {
        public string SensorId { get; set; }
        public DateTimeOffset WindowStart { get; set; }
        public DateTimeOffset WindowEnd { get; set; }
        public double AvgValue { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double StdDevValue { get; set; }
        public int Count { get; set; }
        public string Unit { get; set; }
        public DateTimeOffset ProcessedAt { get; set; }
    }
}
