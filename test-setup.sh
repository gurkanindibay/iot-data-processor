#!/bin/bash

# IoT Data Processor - End-to-End Test Script

echo "🚀 IoT Data Processor - End-to-End Testing"
echo "=========================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to check command success
check_command() {
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✅ $1${NC}"
    else
        echo -e "${RED}❌ $1${NC}"
        exit 1
    fi
}

# Check prerequisites
echo "📋 Checking prerequisites..."

command -v func >/dev/null 2>&1
check_command "Azure Functions Core Tools installed"

command -v dotnet >/dev/null 2>&1
check_command "dotnet CLI installed"

az account show >/dev/null 2>&1
check_command "Azure CLI logged in"

echo ""
echo "🔍 Checking Azure resources..."

# Check Service Bus
az servicebus namespace show \
  --name sb-iot-data-processor-dev \
  --resource-group rg-iot-data-processor-dev \
  --query "name" -o tsv >/dev/null 2>&1
check_command "Service Bus namespace exists"

# Check Storage
az storage account show \
  --name stiotdataprocessordev \
  --resource-group rg-iot-data-processor-dev \
  --query "name" -o tsv >/dev/null 2>&1
check_command "Storage account exists"

# Check containers
az storage container show \
  --account-name stiotdataprocessordev \
  --name processed-data \
  --query "name" -o tsv >/dev/null 2>&1
check_command "Processed data container exists"

az storage container show \
  --account-name stiotdataprocessordev \
  --name anomalies \
  --query "name" -o tsv >/dev/null 2>&1
check_command "Anomalies container exists"

echo ""
echo "🔨 Building projects..."

# Build Functions
cd IoTDataProcessor
dotnet build >/dev/null 2>&1
check_command "Functions project builds successfully"
cd ..

# Build Device Simulator
cd DeviceSimulator
dotnet build >/dev/null 2>&1
check_command "Device simulator builds successfully"
cd ..

echo ""
echo "🧪 Testing Protobuf serialization..."

cd ProtobufTest
dotnet run >/dev/null 2>&1
check_command "Protobuf serialization test passes"
cd ..

echo ""
echo -e "${GREEN}🎉 All prerequisites met! Ready for testing.${NC}"
echo ""
echo "📝 Next steps:"
echo "1. Start functions locally: cd IoTDataProcessor && func start --dotnet-isolated --port 7072"
echo "2. Run device simulator: cd DeviceSimulator && dotnet run"
echo "3. Monitor results in Azure Portal or CLI"
echo ""
echo "📊 Monitoring commands:"
echo "# Check Service Bus messages"
echo "az servicebus topic subscription show --resource-group rg-iot-data-processor-dev --namespace sb-iot-data-processor-dev --topic telemetry-topic --name aggregation-sub --query 'countDetails.activeMessageCount'"
echo ""
echo "# Check processed data"
echo "az storage blob list --account-name stiotdataprocessordev --container processed-data --output table"
echo ""
echo "# Check anomalies"
echo "az storage blob list --account-name stiotdataprocessordev --container anomalies --output table"