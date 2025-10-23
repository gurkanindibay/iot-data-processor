# IoT Data Processor

A complete Azure serverless IoT data processing application that demonstrates efficient telemetry ingestion, real-time processing, and anomaly detection using Azure Functions, Service Bus, and Blob Storage.

## Architecture Overview

```
IoT Devices (MQTT) → Azure IoT Hub → Service Bus Topics → Azure Functions → Blob Storage
                                      ↓
                               Anomaly Detection → Blob Storage (Anomalies)
```

## Components

### 1. Device Simulator (`DeviceSimulator/`)
- **Purpose**: Simulates IoT devices sending telemetry data via MQTT
- **Technology**: MQTTnet library with SAS token authentication
- **Features**:
  - Generates realistic sensor data (temperature, pressure, humidity, vibration)
  - Protobuf serialization for efficient data transmission
  - Configurable device count and message frequency

### 2. Azure Functions (`IoTDataProcessor/`)
- **TelemetryAggregator**: Processes telemetry messages and creates statistical aggregates
- **AnomalyDetector**: Performs threshold-based anomaly detection on sensor readings
- **Technology**: .NET 8 isolated runtime with Service Bus triggers

### 3. Data Schema (`telemetry.proto`)
- **Protocol Buffers**: Efficient binary serialization format
- **Messages**:
  - `Telemetry`: Raw sensor readings with metadata
  - `TelemetryAggregate`: Statistical summaries (avg, min, max, count)
  - `AnomalyAlert`: Detected anomalies with severity levels

## Azure Resources

### Infrastructure (Terraform)
- **Azure IoT Hub**: MQTT device connectivity
- **Azure Service Bus**: Message routing with topics/subscriptions
- **Azure Blob Storage**: Data persistence (processed-data, anomalies containers)
- **Azure Functions**: Serverless processing (consumption plan)
- **Application Insights**: Monitoring and logging

### Message Flow
1. Devices publish telemetry to IoT Hub via MQTT
2. IoT Hub routes messages to Service Bus topic
3. TelemetryAggregator function processes messages and stores aggregates
4. AnomalyDetector function analyzes readings and stores alerts

## Prerequisites

- .NET 8 SDK
- Protocol Buffers compiler (`protoc`)
- Azure CLI
- Terraform

## Setup Instructions

### 1. Infrastructure Deployment
```bash
cd infrastructure
terraform init
terraform plan
terraform apply
```

### 2. Build and Generate Protobuf Classes
```bash
# Install protoc (macOS with Homebrew)
brew install protobuf

# Generate C# classes
protoc --csharp_out=. telemetry.proto --proto_path=.
```

### 3. Configure Local Development
Update `IoTDataProcessor/local.settings.json` with your Azure resource connection strings:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "your-storage-connection-string",
    "ServiceBusConnection": "your-servicebus-connection-string",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

### 4. Run Device Simulator
```bash
cd DeviceSimulator
dotnet run
```

### 5. Deploy Functions Locally
```bash
cd IoTDataProcessor
func start
```

## Configuration

### IoT Hub Device Registration
Devices authenticate using SAS tokens generated from device keys.

### Service Bus Topics
- **telemetry-topic**: Raw telemetry messages
- **Subscriptions**:
  - `aggregation-sub`: For TelemetryAggregator function
  - `anomaly-detection-sub`: For AnomalyDetector function

### Blob Storage Containers
- **processed-data**: JSON aggregates from TelemetryAggregator
- **anomalies**: JSON anomaly alerts from AnomalyDetector

### Anomaly Detection Thresholds
Configurable thresholds by sensor type:
- Temperature: 100°C
- Pressure: 1500 hPa
- Humidity: 100%
- Vibration: 20 mm/s

## Performance Characteristics

- **Throughput**: Designed for 1000+ msgs/sec
- **Latency**: Sub-second processing with Service Bus triggers
- **Storage**: Efficient JSON format with hierarchical blob naming
- **Serialization**: Protobuf reduces payload size by ~70% vs JSON

## Monitoring

- Application Insights integration for function metrics
- Dead-letter queues for failed message processing
- Structured logging with correlation IDs

## Testing

Run the Protobuf serialization tests:
```bash
cd ProtobufTest
dotnet run
```

## Security

- SAS token authentication for IoT devices
- Azure RBAC for resource access
- Network isolation with VNet integration (optional)
- Encrypted data at rest and in transit

## Development Notes

- Functions use isolated runtime for better performance
- Protobuf provides efficient binary serialization
- Service Bus enables reliable message processing
- Blob storage supports hierarchical organization

## Future Enhancements

- Machine learning-based anomaly detection
- Real-time dashboards with SignalR
- Advanced aggregation windows (sliding, tumbling)
- Multi-region deployment with geo-redundancy</content>
<parameter name="filePath">/Users/gurkan_indibay/source/azure_tryouts/azure_functions/README.md