# IoT Data Processor - Fix Roadmap
## Step-by-Step Implementation Plan to Close Gaps

---

## 🎯 Goal
Close the 15% implementation gap to achieve 100% compliance with requirements from `analysis.md` and `development_plan.md`.

---

## 📅 Sprint 1: Core Functionality (Week 1)

### Day 1-2: Batch Processing Implementation

#### Task 1.1: Update TelemetryAggregator for Batch Processing
**File:** `IoTDataProcessor/TelemetryAggregator.cs`  
**Effort:** 2 hours  
**Priority:** 🔴 Critical

```csharp
// Change function signature from:
public async Task Run(
    [ServiceBusTrigger("telemetry-topic", "aggregation-sub", Connection = "ServiceBusConnection")]
    ServiceBusReceivedMessage message,
    ServiceBusMessageActions messageActions)

// To:
public async Task Run(
    [ServiceBusTrigger("telemetry-topic", "aggregation-sub", Connection = "ServiceBusConnection", IsBatched = true)]
    ServiceBusReceivedMessage[] messages,
    ServiceBusMessageActions messageActions)
{
    _logger.LogInformation($"Processing batch of {messages.Length} messages");
    
    foreach (var message in messages)
    {
        try
        {
            // Deserialize and process each message
            var telemetry = Iotdataprocessor.Telemetry.Parser.ParseFrom(message.Body);
            // Add to aggregation buffer (implement in Task 1.3)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing message: {message.MessageId}");
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: ex.Message);
        }
    }
    
    // Complete all successfully processed messages
    foreach (var message in messages)
    {
        await messageActions.CompleteMessageAsync(message);
    }
}
```

**Testing:**
- Send 50 messages
- Verify function processes in batches of 32
- Check Application Insights logs

---

#### Task 1.2: Update AnomalyDetector for Batch Processing
**File:** `IoTDataProcessor/AnomalyDetector.cs`  
**Effort:** 1.5 hours  
**Priority:** 🔴 Critical

```csharp
// Similar changes, batch size = 16
[ServiceBusTrigger("telemetry-topic", "anomaly-detection-sub", Connection = "ServiceBusConnection", IsBatched = true)]
ServiceBusReceivedMessage[] messages
```

---

#### Task 1.3: Implement True Aggregation Logic
**File:** `IoTDataProcessor/AggregationBuffer.cs` (new file)  
**Effort:** 6 hours  
**Priority:** 🔴 Critical

```csharp
namespace IoTDataProcessor
{
    public class AggregationBuffer
    {
        private readonly Dictionary<string, List<TelemetryData>> _buffer = new();
        private readonly TimeSpan _windowDuration = TimeSpan.FromMinutes(5);
        
        public void AddTelemetry(Iotdataprocessor.Telemetry telemetry)
        {
            var key = $"{telemetry.SensorId}";
            if (!_buffer.ContainsKey(key))
            {
                _buffer[key] = new List<TelemetryData>();
            }
            
            _buffer[key].Add(new TelemetryData
            {
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(telemetry.Timestamp),
                Value = telemetry.Value,
                Unit = telemetry.Unit
            });
            
            // Clean old data outside window
            var cutoffTime = DateTimeOffset.UtcNow - _windowDuration;
            _buffer[key] = _buffer[key]
                .Where(t => t.Timestamp >= cutoffTime)
                .ToList();
        }
        
        public Iotdataprocessor.TelemetryAggregate? GetAggregate(string sensorId)
        {
            if (!_buffer.ContainsKey(sensorId) || _buffer[sensorId].Count == 0)
                return null;
            
            var data = _buffer[sensorId];
            var windowStart = data.Min(t => t.Timestamp);
            var windowEnd = data.Max(t => t.Timestamp);
            
            return new Iotdataprocessor.TelemetryAggregate
            {
                SensorId = sensorId,
                WindowStart = windowStart.ToUnixTimeMilliseconds(),
                WindowEnd = windowEnd.ToUnixTimeMilliseconds(),
                AvgValue = data.Average(t => t.Value),
                MinValue = data.Min(t => t.Value),
                MaxValue = data.Max(t => t.Value),
                Count = data.Count,
                Unit = data.First().Unit
            };
        }
    }
    
    internal class TelemetryData
    {
        public DateTimeOffset Timestamp { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
}
```

**Update Program.cs:**
```csharp
services.AddSingleton<AggregationBuffer>();
```

**Update TelemetryAggregator:**
```csharp
private readonly AggregationBuffer _buffer;

public TelemetryAggregator(ILogger<TelemetryAggregator> logger, AggregationBuffer buffer)
{
    _logger = logger;
    _buffer = buffer;
}

// In Run method:
_buffer.AddTelemetry(telemetry);
var aggregate = _buffer.GetAggregate(telemetry.SensorId);
if (aggregate != null)
{
    // Store aggregate
}
```

---

### Day 3-4: Message Routing Filters

#### Task 1.4: Configure Service Bus Subscription Filters
**File:** `main.tf`  
**Effort:** 1 hour  
**Priority:** 🟡 Important

```hcl
# Uncomment and configure filters
resource "azurerm_servicebus_subscription" "aggregation_sub" {
  name               = "aggregation-sub"
  topic_id           = azurerm_servicebus_topic.telemetry_topic.id
  max_delivery_count = 10
  lock_duration      = "PT5M"
}

# Add SQL filter rule
resource "azurerm_servicebus_subscription_rule" "aggregation_filter" {
  name            = "aggregation-filter"
  subscription_id = azurerm_servicebus_subscription.aggregation_sub.id
  filter_type     = "SqlFilter"
  sql_filter      = "processingType = 'aggregate' OR processingType IS NULL"
}

resource "azurerm_servicebus_subscription_rule" "anomaly_filter" {
  name            = "anomaly-filter"
  subscription_id = azurerm_servicebus_subscription.anomaly_detection_sub.id
  filter_type     = "SqlFilter"
  sql_filter      = "processingType = 'anomaly' OR processingType IS NULL"
}
```

**Update Device Simulator:**
```csharp
// In SendTelemetryMessage method, add message properties
var message = new MqttApplicationMessageBuilder()
    .WithTopic("devices/" + _deviceId + "/messages/events/")
    .WithPayload(messageBytes)
    .WithUserProperty("processingType", _random.Next(2) == 0 ? "aggregate" : "anomaly")
    .Build();
```

---

#### Task 1.5: Configure IoT Hub Routing Rules
**File:** `main.tf`  
**Effort:** 1 hour  
**Priority:** 🟡 Important

```hcl
# Update IoT Hub Route
resource "azurerm_iothub_route" "telemetry_route_priority" {
  resource_group_name = azurerm_resource_group.rg.name
  iothub_name         = azurerm_iothub.iot_hub.name
  name                = "high-priority-route"
  
  source         = "DeviceMessages"
  condition      = "priority = 'high'"
  endpoint_names = [azurerm_iothub_endpoint_servicebus_topic.servicebus_endpoint.name]
  enabled        = true
}

resource "azurerm_iothub_route" "telemetry_route_default" {
  resource_group_name = azurerm_resource_group.rg.name
  iothub_name         = azurerm_iothub.iot_hub.name
  name                = "default-route"
  
  source         = "DeviceMessages"
  condition      = "true"
  endpoint_names = [azurerm_iothub_endpoint_servicebus_topic.servicebus_endpoint.name]
  enabled        = true
}
```

---

### Day 5: Device Simulator Enhancements

#### Task 1.6: Make Simulator Frequency Configurable
**File:** `DeviceSimulator/Program.cs`  
**Effort:** 2 hours  
**Priority:** 🟡 Important

```csharp
private static int _messageIntervalMs = 5000; // Default 5 seconds
private static int _deviceCount = 1;
private static int _messagesPerSecond = 0; // 0 = use interval

static async Task Main(string[] args)
{
    // Parse arguments
    if (args.Length >= 4) int.TryParse(args[3], out _messageIntervalMs);
    if (args.Length >= 5) int.TryParse(args[4], out _messagesPerSecond);
    if (args.Length >= 6) int.TryParse(args[5], out _deviceCount);
    
    // Calculate interval from messages per second if specified
    if (_messagesPerSecond > 0)
    {
        _messageIntervalMs = 1000 / _messagesPerSecond;
    }
    
    Console.WriteLine($"Configuration:");
    Console.WriteLine($"  Device Count: {_deviceCount}");
    Console.WriteLine($"  Messages/Second: {_messagesPerSecond}");
    Console.WriteLine($"  Interval: {_messageIntervalMs}ms");
    
    // Start multiple device simulators
    var tasks = new List<Task>();
    for (int i = 0; i < _deviceCount; i++)
    {
        var deviceId = $"simulated-device-{i:D3}";
        tasks.Add(RunDeviceSimulator(deviceId, cts.Token));
    }
    
    await Task.WhenAll(tasks);
}

static async Task RunDeviceSimulator(string deviceId, CancellationToken cancellationToken)
{
    // Similar to current Main, but with deviceId parameter
}
```

**Usage:**
```bash
# Single device, 1 message every 5 seconds
dotnet run

# 10 devices, 10 messages per second each (100 total msgs/sec)
dotnet run -- simulated-device-001 iot-hub.azure-devices.net device-key 100 10 10

# 100 devices, 10 messages per second each (1000 total msgs/sec)
dotnet run -- simulated-device-001 iot-hub.azure-devices.net device-key 100 10 100
```

---

## 📅 Sprint 2: Testing & Security (Week 2)

### Day 1-3: Unit Testing

#### Task 2.1: Create Test Project
**Effort:** 1 hour  
**Priority:** 🔴 Critical

```bash
cd IoTDataProcessor
dotnet new xunit -n IoTDataProcessor.Tests
cd IoTDataProcessor.Tests
dotnet add reference ../IoTDataProcessor.csproj
dotnet add package Moq
dotnet add package FluentAssertions
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.ServiceBus
```

---

#### Task 2.2: Write Core Unit Tests
**File:** `IoTDataProcessor.Tests/TelemetryAggregatorTests.cs`  
**Effort:** 8 hours  
**Priority:** 🔴 Critical

```csharp
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Google.Protobuf;

namespace IoTDataProcessor.Tests
{
    public class AggregationBufferTests
    {
        [Fact]
        public void AddTelemetry_SingleMessage_CreatesBuffer()
        {
            // Arrange
            var buffer = new AggregationBuffer();
            var telemetry = new Iotdataprocessor.Telemetry
            {
                SensorId = "sensor-001",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Value = 25.5,
                Unit = "celsius"
            };
            
            // Act
            buffer.AddTelemetry(telemetry);
            var aggregate = buffer.GetAggregate("sensor-001");
            
            // Assert
            aggregate.Should().NotBeNull();
            aggregate.Count.Should().Be(1);
            aggregate.AvgValue.Should().Be(25.5);
        }
        
        [Fact]
        public void GetAggregate_MultipleMessages_CalculatesCorrectly()
        {
            // Arrange
            var buffer = new AggregationBuffer();
            var sensorId = "sensor-001";
            var values = new[] { 10.0, 20.0, 30.0, 40.0, 50.0 };
            
            foreach (var value in values)
            {
                buffer.AddTelemetry(new Iotdataprocessor.Telemetry
                {
                    SensorId = sensorId,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Value = value,
                    Unit = "celsius"
                });
            }
            
            // Act
            var aggregate = buffer.GetAggregate(sensorId);
            
            // Assert
            aggregate.Should().NotBeNull();
            aggregate.Count.Should().Be(5);
            aggregate.AvgValue.Should().Be(30.0);
            aggregate.MinValue.Should().Be(10.0);
            aggregate.MaxValue.Should().Be(50.0);
        }
    }
    
    public class AnomalyDetectorTests
    {
        [Theory]
        [InlineData("temperature", 150.0, true)]  // Above threshold
        [InlineData("temperature", 50.0, false)]  // Below threshold
        [InlineData("pressure", 2000.0, true)]
        [InlineData("humidity", 80.0, false)]
        public void IsAnomalous_VariousValues_ReturnsCorrectResult(
            string sensorType, double value, bool expectedAnomaly)
        {
            // Arrange
            var logger = new Mock<ILogger<AnomalyDetector>>();
            var detector = new AnomalyDetector(logger.Object);
            var telemetry = new Iotdataprocessor.Telemetry
            {
                SensorId = $"sensor-{sensorType}",
                Value = value
            };
            
            // Act
            var isAnomaly = detector.IsAnomalous(telemetry);
            
            // Assert
            isAnomaly.Should().Be(expectedAnomaly);
        }
    }
    
    public class ProtobufSerializationTests
    {
        [Fact]
        public void Serialize_Deserialize_PreservesData()
        {
            // Arrange
            var original = new Iotdataprocessor.Telemetry
            {
                SensorId = "sensor-001",
                Timestamp = 1234567890123,
                Value = 25.5,
                Unit = "celsius"
            };
            original.Metadata.Add("key", "value");
            
            // Act
            var bytes = original.ToByteArray();
            var deserialized = Iotdataprocessor.Telemetry.Parser.ParseFrom(bytes);
            
            // Assert
            deserialized.SensorId.Should().Be(original.SensorId);
            deserialized.Timestamp.Should().Be(original.Timestamp);
            deserialized.Value.Should().Be(original.Value);
            deserialized.Unit.Should().Be(original.Unit);
            deserialized.Metadata["key"].Should().Be("value");
        }
    }
}
```

**Run tests:**
```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

### Day 4-5: Managed Identity Authentication

#### Task 2.3: Convert Functions to Use Managed Identity
**File:** `IoTDataProcessor/Program.cs`  
**Effort:** 3 hours  
**Priority:** 🔴 Critical

```csharp
using Azure.Identity;

services.AddAzureClients(builder =>
{
    // Service Bus with Managed Identity
    var serviceBusNamespace = Environment.GetEnvironmentVariable("ServiceBusNamespace");
    builder.AddServiceBusClient($"{serviceBusNamespace}.servicebus.windows.net")
        .WithCredential(new DefaultAzureCredential());
    
    // Blob Storage with Managed Identity
    var storageAccountName = Environment.GetEnvironmentVariable("StorageAccountName");
    builder.AddBlobServiceClient(new Uri($"https://{storageAccountName}.blob.core.windows.net"))
        .WithCredential(new DefaultAzureCredential());
});
```

**Update Terraform `main.tf`:**
```hcl
resource "azurerm_function_app_flex_consumption" "func_app" {
  # ... existing config ...
  
  app_settings = {
    # Remove connection strings
    # "AzureWebJobsStorage" = azurerm_storage_account.storage.primary_connection_string
    # "ServiceBusConnection" = "..."
    
    # Add namespace/account names
    "StorageAccountName" = azurerm_storage_account.storage.name
    "ServiceBusNamespace" = azurerm_servicebus_namespace.servicebus.name
    
    # Keep identity-based connection
    "ServiceBusConnection__fullyQualifiedNamespace" = "${azurerm_servicebus_namespace.servicebus.name}.servicebus.windows.net"
  }
}
```

**Update Functions:**
```csharp
// Inject BlobServiceClient
public class TelemetryAggregator
{
    private readonly ILogger<TelemetryAggregator> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    
    public TelemetryAggregator(
        ILogger<TelemetryAggregator> logger,
        BlobServiceClient blobServiceClient)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
    }
    
    // Use injected client instead of creating new one
    var containerClient = _blobServiceClient.GetBlobContainerClient("processed-data");
}
```

---

## 📅 Sprint 3: Monitoring & Performance (Week 3)

### Day 1-2: Custom Metrics

#### Task 3.1: Add Application Insights Custom Metrics
**File:** `IoTDataProcessor/Program.cs`  
**Effort:** 2 hours  
**Priority:** 🟡 Important

```csharp
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

services.AddSingleton<TelemetryClient>();
```

**Update TelemetryAggregator:**
```csharp
private readonly TelemetryClient _telemetryClient;

public TelemetryAggregator(
    ILogger<TelemetryAggregator> logger,
    BlobServiceClient blobServiceClient,
    TelemetryClient telemetryClient)
{
    _logger = logger;
    _blobServiceClient = blobServiceClient;
    _telemetryClient = telemetryClient;
}

public async Task Run(...)
{
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        // Process messages
        _telemetryClient.TrackMetric("MessagesProcessed", messages.Length);
    }
    finally
    {
        stopwatch.Stop();
        _telemetryClient.TrackMetric("ProcessingLatencyMs", stopwatch.ElapsedMilliseconds);
        _telemetryClient.TrackMetric("MessagesPerSecond", messages.Length / (stopwatch.ElapsedMilliseconds / 1000.0));
    }
}
```

---

#### Task 3.2: Configure Monitoring Alerts
**File:** `main.tf` (add at end)  
**Effort:** 2 hours  
**Priority:** 🟡 Important

```hcl
# Metric Alert for Error Rate
resource "azurerm_monitor_metric_alert" "function_error_rate" {
  name                = "alert-function-error-rate"
  resource_group_name = azurerm_resource_group.rg.name
  scopes              = [azurerm_function_app_flex_consumption.func_app.id]
  description         = "Alert when function error rate exceeds 5%"
  
  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "Http5xx"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 5
  }
  
  window_size        = "PT5M"
  frequency          = "PT1M"
  severity           = 2
  
  action {
    action_group_id = azurerm_monitor_action_group.alerts.id
  }
}

# Metric Alert for Processing Latency
resource "azurerm_monitor_metric_alert" "processing_latency" {
  name                = "alert-processing-latency"
  resource_group_name = azurerm_resource_group.rg.name
  scopes              = [azurerm_application_insights.app_insights.id]
  description         = "Alert when processing latency exceeds 10 seconds"
  
  criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "customMetrics/ProcessingLatencyMs"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 10000
  }
  
  window_size        = "PT5M"
  frequency          = "PT1M"
  severity           = 2
  
  action {
    action_group_id = azurerm_monitor_action_group.alerts.id
  }
}

# Action Group for Alerts
resource "azurerm_monitor_action_group" "alerts" {
  name                = "ag-iot-data-processor-alerts"
  resource_group_name = azurerm_resource_group.rg.name
  short_name          = "iot-alerts"
  
  email_receiver {
    name          = "sendtoadmin"
    email_address = "admin@example.com"
  }
}
```

---

### Day 3-5: Performance Testing

#### Task 3.3: Create Load Testing Script
**File:** `scripts/load-test.sh` (new file)  
**Effort:** 4 hours  
**Priority:** 🟡 Important

```bash
#!/bin/bash

# Load Test Configuration
DEVICE_COUNT=100
MESSAGES_PER_SECOND=10
DURATION_MINUTES=10
TOTAL_DEVICES=10

echo "Starting load test..."
echo "Target: $((DEVICE_COUNT * MESSAGES_PER_SECOND)) msgs/sec"
echo "Duration: $DURATION_MINUTES minutes"

# Start device simulators in parallel
for i in $(seq 1 $TOTAL_DEVICES); do
    dotnet run --project DeviceSimulator/DeviceSimulator.csproj \
        -- "simulated-device-$(printf '%03d' $i)" \
        "iot-iot-data-processor-dev.azure-devices.net" \
        "$DEVICE_KEY" \
        $MESSAGES_PER_SECOND \
        $DEVICE_COUNT &
    
    echo "Started device simulator $i"
done

# Wait for duration
sleep $((DURATION_MINUTES * 60))

# Stop all simulators
pkill -f "DeviceSimulator"

echo "Load test complete!"
echo "Check Application Insights for results"
```

**Performance Test Checklist:**
- [ ] Test 100 msgs/sec (warm-up)
- [ ] Test 500 msgs/sec (sustained)
- [ ] Test 1000 msgs/sec (target)
- [ ] Test 2000 msgs/sec (spike)
- [ ] Measure end-to-end latency (p50, p95, p99)
- [ ] Monitor error rate
- [ ] Check auto-scaling behavior
- [ ] Validate data integrity

---

## 📅 Sprint 4: Documentation & Polish (Week 4)

### Day 1-2: Architecture Documentation

#### Task 4.1: Create Professional Architecture Diagram
**Tool:** draw.io, Lucidchart, or Azure Architecture icons  
**Effort:** 3 hours  
**Priority:** 🟢 Nice to Have

**Diagrams to Create:**
1. **High-Level Architecture** - Component overview
2. **Data Flow Diagram** - Message routing paths
3. **Security Architecture** - Identity and access
4. **Deployment Architecture** - Resource organization

---

#### Task 4.2: Write Performance Test Results
**File:** `PERFORMANCE_TESTING.md` (new)  
**Effort:** 2 hours  
**Priority:** 🟡 Important

Include:
- Test configuration
- Results table (throughput, latency, errors)
- Graphs/screenshots
- Bottleneck analysis
- Recommendations

---

### Day 3-4: Demo Video

#### Task 4.3: Record Demo Video
**Effort:** 4 hours  
**Priority:** 🟢 Nice to Have

**Video Structure (8-10 minutes):**
1. Introduction (1 min) - Project overview
2. Architecture (2 min) - Diagram walkthrough
3. Code Review (3 min) - Key components
4. Live Demo (3 min) - End-to-end flow
5. Results (1 min) - Performance metrics

**Tools:** OBS Studio, Loom, or Camtasia

---

### Day 5: Final Polish

#### Task 4.4: Update Documentation
**Effort:** 2 hours  
**Priority:** 🟡 Important

- [ ] Update README.md with latest features
- [ ] Add PERFORMANCE_TESTING.md
- [ ] Add API_DOCUMENTATION.md
- [ ] Update TESTING_GUIDE.md
- [ ] Create DEPLOYMENT_GUIDE.md
- [ ] Add troubleshooting section

---

## 📊 Progress Tracking

### Sprint 1 Checklist
- [ ] Task 1.1: Batch processing (TelemetryAggregator)
- [ ] Task 1.2: Batch processing (AnomalyDetector)
- [ ] Task 1.3: True aggregation logic
- [ ] Task 1.4: Service Bus filters
- [ ] Task 1.5: IoT Hub routing rules
- [ ] Task 1.6: Simulator enhancements

### Sprint 2 Checklist
- [ ] Task 2.1: Create test project
- [ ] Task 2.2: Write unit tests (>80% coverage)
- [ ] Task 2.3: Managed identity conversion

### Sprint 3 Checklist
- [ ] Task 3.1: Custom metrics
- [ ] Task 3.2: Monitoring alerts
- [ ] Task 3.3: Performance testing

### Sprint 4 Checklist
- [ ] Task 4.1: Architecture diagrams
- [ ] Task 4.2: Performance documentation
- [ ] Task 4.3: Demo video
- [ ] Task 4.4: Final documentation

---

## 🎯 Success Criteria

### Definition of Done
- [ ] All Priority 1 (🔴) tasks completed
- [ ] Unit tests passing with >80% coverage
- [ ] Performance tests validate 1000 msgs/sec target
- [ ] All documentation updated
- [ ] Code reviewed and merged
- [ ] Deployed to production environment

### Verification Steps
1. Run full test suite: `dotnet test`
2. Run load test: `./scripts/load-test.sh`
3. Check Application Insights dashboards
4. Verify no connection strings in configuration
5. Review security scan results
6. Demo working end-to-end

---

## 📈 Expected Outcomes

### Before Fix
- Implementation: 85%
- Performance: Untested
- Tests: 0% coverage
- Security: Connection strings
- Batch Size: 1 message

### After Fix
- Implementation: 100% ✅
- Performance: 1000+ msgs/sec validated ✅
- Tests: >80% coverage ✅
- Security: Managed identity only ✅
- Batch Size: 32 messages (aggregation), 16 (anomaly) ✅

---

## 🚀 Getting Started

```bash
# 1. Create feature branch
git checkout -b feature/close-implementation-gaps

# 2. Start with Sprint 1, Task 1.1
cd IoTDataProcessor
code TelemetryAggregator.cs

# 3. Make changes, test locally
func start

# 4. Commit and push
git add .
git commit -m "feat: implement batch processing"
git push origin feature/close-implementation-gaps

# 5. Repeat for each task
```

---

**Roadmap Created:** 2024  
**Estimated Total Effort:** 60-80 hours  
**Recommended Pace:** 4 weeks (20 hours/week)  
**Priority:** Start with Sprint 1 (Critical fixes)
