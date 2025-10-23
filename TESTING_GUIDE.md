# IoT Data Processor - Testing Guide

## Current Status & Limitations

✅ **Successfully Deployed Infrastructure:**
- Azure Service Bus (namespace, topic, subscriptions)
- Azure Storage Account (with containers: processed-data, anomalies, raw-telemetry)
- Application Insights (monitoring)
- Resource Group

❌ **Blocked by Quota:**
- Azure Functions App (both Consumption Y1 and Premium P1v3 plans)
- Current limit for both plan types: 0

## Working Solution: Local Development with Cloud Infrastructure

Since cloud deployment is blocked by quotas, we can fully test the IoT data processing pipeline using:

- **Local Azure Functions** (running on your machine)
- **Cloud Service Bus & Storage** (already deployed)
- **Device Simulator** (local MQTT client)

## Setup Instructions

### 1. Prerequisites
```bash
# Install Azure Functions Core Tools
npm install -g azure-functions-core-tools@4 --unsafe-perm true

# Verify installation
func --version
```

### 2. Configure Local Functions
The `local.settings.json` is already configured with your cloud resources:

```json
{
  "AzureWebJobsStorage": "DefaultEndpointsProtocol=https;...",
  "ServiceBusConnection": "Endpoint=sb://sb-iot-data-processor-dev.servicebus.windows.net/...",
  "APPLICATIONINSIGHTS_CONNECTION_STRING": "InstrumentationKey=...",
  "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
}
```

### 3. Test Functions Locally
```bash
cd IoTDataProcessor

# Start functions (use different port if 7071 is busy)
func start --dotnet-isolated --port 7072
```

Expected output:
```
Azure Functions Core Tools
Core Tools Version: 4.3.0
Function Runtime Version: 4.27.0.21532

Functions:
        TelemetryAggregator: serviceBusTrigger
        AnomalyDetector: serviceBusTrigger

For detailed output, run func with --verbose flag
```

### 4. Test Device Simulator
```bash
cd DeviceSimulator
dotnet run
```

This will:
- Connect to Azure IoT Hub via MQTT
- Send Protobuf-encoded telemetry messages
- Messages flow: IoT Hub → Service Bus → Local Functions → Storage

### 5. Monitor Results

**Check Service Bus Messages:**
```bash
az servicebus topic subscription show \
  --resource-group rg-iot-data-processor-dev \
  --namespace sb-iot-data-processor-dev \
  --topic telemetry-topic \
  --name aggregation-sub \
  --query "countDetails.activeMessageCount"
```

**Check Storage Results:**
```bash
# List processed data
az storage blob list \
  --account-name stiotdataprocessordev \
  --container processed-data \
  --output table

# List anomalies
az storage blob list \
  --account-name stiotdataprocessordev \
  --container anomalies \
  --output table
```

**View Application Insights:**
```bash
az monitor app-insights events show \
  --app ai-iot-data-processor-dev \
  --resource-group rg-iot-data-processor-dev \
  --types traces \
  --output table
```

## End-to-End Testing Workflow

1. **Start Functions Locally:**
   ```bash
   cd IoTDataProcessor && func start --dotnet-isolated --port 7072
   ```

2. **Run Device Simulator:**
   ```bash
   cd DeviceSimulator && dotnet run
   ```

3. **Monitor Logs:**
   - Functions console will show message processing
   - Check Application Insights for telemetry
   - Verify blobs are created in Storage

4. **Validate Data Flow:**
   - Raw telemetry → Service Bus topic
   - TelemetryAggregator processes → JSON aggregates in `processed-data` container
   - AnomalyDetector analyzes → JSON alerts in `anomalies` container

## Performance Testing

### Single Device Test
```bash
cd DeviceSimulator
dotnet run -- --device-count 1 --message-interval 1000
```

### Multi-Device Load Test
```bash
cd DeviceSimulator
dotnet run -- --device-count 10 --message-interval 100
```

Monitor throughput and latency through Application Insights.

## Troubleshooting

### Functions Won't Start
```bash
# Check for port conflicts
lsof -i :7071

# Try different port
func start --dotnet-isolated --port 7073
```

### Connection Issues
```bash
# Test Service Bus connection
az servicebus namespace show --name sb-iot-data-processor-dev --resource-group rg-iot-data-processor-dev

# Test Storage connection
az storage account show --name stiotdataprocessordev --resource-group rg-iot-data-processor-dev
```

### Protobuf Issues
```bash
# Test serialization
cd ProtobufTest && dotnet run
```

## Alternative Deployment Options

If quota issues persist, consider:

### Option A: Different Azure Region
```terraform
variable "location" {
  default = "West US 2"  # Try different region
}
```

### Option B: App Service Plan
```terraform
resource "azurerm_service_plan" "func_plan" {
  sku_name = "S1"  # Standard App Service Plan
}
```

### Option C: Request Quota Increase
- Azure Portal → Subscriptions → Usage + quotas → Request increase
- Request Premium v3 VMs or Function Apps quota

## Summary

This setup provides **full functionality testing** of your IoT Data Processor with:
- ✅ Protobuf serialization/deserialization
- ✅ Service Bus message processing
- ✅ Blob Storage persistence
- ✅ Anomaly detection logic
- ✅ Application Insights monitoring
- ✅ End-to-end data pipeline validation

The only limitation is cloud hosting of functions, but local development provides complete testing capability.