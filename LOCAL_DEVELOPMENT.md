# Local Development Setup

Since Azure Functions deployment is blocked by quota limitations, here are several approaches to work with the current infrastructure:

## Option 1: Local Functions Development (Recommended)

### Prerequisites
- .NET 8 SDK
- Azure Functions Core Tools
- Azure CLI

### Setup Steps

1. **Install Azure Functions Core Tools:**
```bash
# macOS with Homebrew
brew tap azure/functions
brew install azure-functions-core-tools@4
```

2. **Configure Local Settings:**
Update `IoTDataProcessor/local.settings.json` with your connection strings:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "your-storage-connection-string",
    "ServiceBusConnection": "your-servicebus-connection-string",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": "your-app-insights-connection-string"
  }
}
```

3. **Run Functions Locally:**
```bash
cd IoTDataProcessor
func start
```

4. **Test End-to-End:**
- Run the Device Simulator: `cd DeviceSimulator && dotnet run`
- Monitor Service Bus messages and Blob Storage output
- Check Application Insights for telemetry

## Option 2: Deploy to Different Region

If quota is region-specific, deploy to a different Azure region:

```bash
# Update main.tf
variable "location" {
  default = "West US 2"  # Try different region
}
```

## Option 3: Use Azure Container Instances

Deploy functions as containers:

```bash
# Build and push container
az acr build --registry myregistry --image iot-functions:v1 .

# Deploy to ACI
az container create --resource-group rg-iot-data-processor-dev \
  --name iot-functions-container \
  --image myregistry.azurecr.io/iot-functions:v1 \
  --cpu 1 --memory 1 \
  --environment-variables ServiceBusConnection=$SB_CONN StorageConnection=$STORAGE_CONN
```

## Option 4: Premium Plan (Already Updated)

The Terraform configuration has been updated to use Premium Plan (P1v3) instead of Consumption Plan, which has more predictable quotas.

## Current Status

✅ **Successfully Deployed:**
- Resource Group
- Service Bus (namespace, topic, subscriptions)
- Storage Account (with containers and lifecycle policies)
- Application Insights

❌ **Blocked by Quota:**
- Functions App (consumption plan limits)

## Testing Strategy

1. **Local Development:** Use `func start` for local testing
2. **Integration Testing:** Test with real Service Bus and Storage
3. **Performance Testing:** Simulate load with multiple device simulators
4. **Monitoring:** Use Application Insights for observability

## Next Steps

1. Get local development working
2. Test device simulator → Service Bus → local functions → Storage
3. Validate Protobuf serialization/deserialization
4. Measure performance and optimize
5. Plan production deployment strategy