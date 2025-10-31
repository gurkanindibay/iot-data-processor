using System.Text.Json;
using Google.Protobuf;
using Azure.Messaging.ServiceBus;

Console.WriteLine("Running IoT Data Processor Protobuf Tests...");
Console.WriteLine();

// Create a sample telemetry message
var telemetry = new Iotdataprocessor.Telemetry
{
    SensorId = "temperature-sensor-001",
    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    Value = 25.5,
    Unit = "celsius"
};

// Add some metadata
telemetry.Metadata.Add("location", "factory-floor-1");
telemetry.Metadata.Add("device_type", "industrial-sensor");

// Serialize to bytes using extension method
var bytes = telemetry.ToByteArray();
Console.WriteLine($"Serialized size: {bytes.Length} bytes");
Console.WriteLine($"Base64: {Convert.ToBase64String(bytes)}");

// Deserialize back
var deserialized = Iotdataprocessor.Telemetry.Parser.ParseFrom(bytes);

// Verify
Console.WriteLine($"Sensor ID: {deserialized.SensorId}");
Console.WriteLine($"Value: {deserialized.Value} {deserialized.Unit}");
Console.WriteLine($"Timestamp: {DateTimeOffset.FromUnixTimeMilliseconds(deserialized.Timestamp)}");
Console.WriteLine($"Location: {deserialized.Metadata["location"]}");

// Test JSON serialization for storage
var jsonData = JsonSerializer.Serialize(new
{
    sensorId = deserialized.SensorId,
    timestamp = DateTimeOffset.FromUnixTimeMilliseconds(deserialized.Timestamp),
    value = deserialized.Value,
    unit = deserialized.Unit,
    metadata = deserialized.Metadata
});

Console.WriteLine($"JSON: {jsonData}");

// Test AnomalyAlert
var anomaly = new Iotdataprocessor.AnomalyAlert
{
    SensorId = telemetry.SensorId,
    Timestamp = telemetry.Timestamp,
    Value = telemetry.Value,
    AnomalyType = "threshold_exceeded",
    Threshold = 30.0,
    Severity = "high"
};

var anomalyBytes = anomaly.ToByteArray();
var anomalyDeserialized = Iotdataprocessor.AnomalyAlert.Parser.ParseFrom(anomalyBytes);

Console.WriteLine($"Anomaly: {anomalyDeserialized.SensorId} - {anomalyDeserialized.AnomalyType} ({anomalyDeserialized.Severity})");

Console.WriteLine();
Console.WriteLine("All Protobuf tests passed!");

// Send the message to Service Bus
await using var client = new ServiceBusClient(""); // Replace with actual connection string
var sender = client.CreateSender("telemetry-topic");
var message = new ServiceBusMessage(bytes);
await sender.SendMessageAsync(message);
Console.WriteLine("Message sent to Service Bus topic.");
