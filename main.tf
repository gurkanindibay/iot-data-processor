# IoT Data Processor Infrastructure - Terraform Configuration
# Naming Convention: {abbreviation}-{project}-{environment}
# Examples: rg-iot-data-processor-dev, iot-iot-data-processor-dev, sb-iot-data-processor-dev

terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

# Variables
variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
  default     = "rg-iot-data-processor-dev"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "East US"
}

variable "environment" {
  description = "Environment name"
  type        = string
  default     = "dev"
}

variable "iot_hub_name" {
  description = "Name of the IoT Hub"
  type        = string
  default     = "iot-iot-data-processor-dev"
}

variable "servicebus_namespace_name" {
  description = "Name of the Service Bus namespace"
  type        = string
  default     = "sb-iot-data-processor-dev"
}

variable "storage_account_name" {
  description = "Name of the Storage Account (lowercase, no hyphens)"
  type        = string
  default     = "stiotdataprocessordev"
}

variable "app_insights_name" {
  description = "Name of the Application Insights"
  type        = string
  default     = "ai-iot-data-processor-dev"
}

# Resource Group
resource "azurerm_resource_group" "rg" {
  name     = var.resource_group_name
  location = var.location

  tags = {
    Environment = var.environment
    Project     = "IoT Data Processor"
  }
}

# IoT Hub
resource "azurerm_iot_hub" "iot_hub" {
  name                = var.iot_hub_name
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location

  sku {
    name     = "S1"
    capacity = "2"
  }

  tags = {
    Environment = var.environment
    Project     = "IoT Data Processor"
  }
}

# Service Bus Namespace
resource "azurerm_servicebus_namespace" "servicebus" {
  name                = var.servicebus_namespace_name
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = "Standard"

  tags = {
    Environment = var.environment
    Project     = "IoT Data Processor"
  }
}

# Service Bus Topic
resource "azurerm_servicebus_topic" "telemetry_topic" {
  name         = "telemetry-topic"
  namespace_id = azurerm_servicebus_namespace.servicebus.id
}

# Service Bus Subscriptions
resource "azurerm_servicebus_subscription" "aggregation_sub" {
  name               = "aggregation-sub"
  topic_id           = azurerm_servicebus_topic.telemetry_topic.id
  max_delivery_count = 10

  # Optional: Add filters if needed
  # filter {
  #   sql_expression = "processingType = 'aggregate'"
  # }
}

resource "azurerm_servicebus_subscription" "anomaly_detection_sub" {
  name               = "anomaly-detection-sub"
  topic_id           = azurerm_servicebus_topic.telemetry_topic.id
  max_delivery_count = 10

  # Optional: Add filters if needed
  # filter {
  #   sql_expression = "processingType = 'anomaly'"
  # }
}

resource "azurerm_servicebus_subscription" "archival_sub" {
  name               = "archival-sub"
  topic_id           = azurerm_servicebus_topic.telemetry_topic.id
  max_delivery_count = 10

  # Optional: Add filters if needed
  # filter {
  #   sql_expression = "priority = 'low'"
  # }
}

# Storage Account
resource "azurerm_storage_account" "storage" {
  name                     = var.storage_account_name
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "GRS"
  account_kind             = "StorageV2"

  tags = {
    Environment = var.environment
    Project     = "IoT Data Processor"
  }
}

# Storage Containers
resource "azurerm_storage_container" "processed_data" {
  name                  = "processed-data"
  storage_account_name  = azurerm_storage_account.storage.name
  container_access_type = "private"
}

resource "azurerm_storage_container" "anomalies" {
  name                  = "anomalies"
  storage_account_name  = azurerm_storage_account.storage.name
  container_access_type = "private"
}

resource "azurerm_storage_container" "raw_telemetry" {
  name                  = "raw-telemetry"
  storage_account_name  = azurerm_storage_account.storage.name
  container_access_type = "private"
}

# Storage Lifecycle Management
resource "azurerm_storage_management_policy" "lifecycle" {
  storage_account_id = azurerm_storage_account.storage.id

  rule {
    name    = "data_lifecycle"
    enabled = true
    filters {
      prefix_match = ["processed-data/", "anomalies/", "raw-telemetry/"]
      blob_types   = ["blockBlob"]
    }
    actions {
      base_blob {
        tier_to_cool_after_days_since_modification_greater_than    = 90
        tier_to_archive_after_days_since_modification_greater_than = 365
        delete_after_days_since_modification_greater_than          = 2555  # 7 years
      }
      snapshot {
        delete_after_days_since_creation_greater_than = 365
      }
    }
  }
}

# Application Insights
resource "azurerm_application_insights" "app_insights" {
  name                = var.app_insights_name
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  application_type    = "web"

  retention_in_days = 90

  tags = {
    Environment = var.environment
    Project     = "IoT Data Processor"
  }
}

# IoT Hub Routing to Service Bus
resource "azurerm_iot_hub_endpoint_servicebus_topic" "routing_endpoint" {
  resource_group_name = azurerm_resource_group.rg.name
  iot_hub_name        = azurerm_iot_hub.iot_hub.name
  name                = "telemetry-routing"

  servicebus_topic_id = azurerm_servicebus_topic.telemetry_topic.id
}

resource "azurerm_iot_hub_route" "telemetry_route" {
  resource_group_name = azurerm_resource_group.rg.name
  iot_hub_name        = azurerm_iot_hub.iot_hub.name
  name                = "telemetry-route"

  source         = "DeviceMessages"
  condition      = "true"  # Route all messages
  endpoint_names = [azurerm_iot_hub_endpoint_servicebus_topic.routing_endpoint.name]
  enabled        = true
}

# Outputs
output "resource_group_name" {
  description = "Name of the resource group"
  value       = azurerm_resource_group.rg.name
}

output "iot_hub_name" {
  description = "Name of the IoT Hub"
  value       = azurerm_iot_hub.iot_hub.name
}

output "iot_hub_connection_string" {
  description = "IoT Hub connection string"
  value       = azurerm_iot_hub.iot_hub.connection_string
  sensitive   = true
}

output "servicebus_namespace_name" {
  description = "Name of the Service Bus namespace"
  value       = azurerm_servicebus_namespace.servicebus.name
}

output "servicebus_topic_name" {
  description = "Name of the Service Bus topic"
  value       = azurerm_servicebus_topic.telemetry_topic.name
}

output "storage_account_name" {
  description = "Name of the Storage Account"
  value       = azurerm_storage_account.storage.name
}

output "storage_account_connection_string" {
  description = "Storage Account connection string"
  value       = azurerm_storage_account.storage.primary_connection_string
  sensitive   = true
}

output "app_insights_instrumentation_key" {
  description = "Application Insights instrumentation key"
  value       = azurerm_application_insights.app_insights.instrumentation_key
  sensitive   = true
}

output "app_insights_connection_string" {
  description = "Application Insights connection string"
  value       = azurerm_application_insights.app_insights.connection_string
  sensitive   = true
}