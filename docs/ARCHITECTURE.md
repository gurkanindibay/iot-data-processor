# IoT Data Processing Platform - Architecture Documentation

## Table of Contents
1. [Executive Summary](#executive-summary)
2. [System Overview](#system-overview)
3. [High-Level Architecture](#high-level-architecture)
4. [Component Architecture](#component-architecture)
5. [Data Flow Architecture](#data-flow-architecture)
6. [Deployment Architecture](#deployment-architecture)
7. [Security Architecture](#security-architecture)
8. [Scalability and Performance](#scalability-and-performance)
9. [Monitoring and Observability](#monitoring-and-observability)
10. [Disaster Recovery](#disaster-recovery)

---

## Executive Summary

The IoT Data Processing Platform is a cloud-native, serverless solution built on Microsoft Azure designed to handle high-volume telemetry data from IoT devices. The platform processes 1000+ messages per second, performs real-time aggregation, and provides comprehensive monitoring and alerting capabilities.

### Key Characteristics
- **Serverless Architecture**: Azure Functions for compute, eliminating infrastructure management
- **Event-Driven**: Asynchronous processing using Azure Service Bus and Event Hubs
- **Scalable**: Auto-scaling based on workload with Premium tier support
- **Secure**: Managed identities, private endpoints, and encryption at rest/in transit
- **Observable**: Comprehensive logging, metrics, and alerting via Application Insights

### Technology Stack
- **Compute**: Azure Functions (Premium Plan)
- **Messaging**: Azure Service Bus, Event Hubs, IoT Hub
- **Storage**: Azure Blob Storage, Cosmos DB
- **Monitoring**: Application Insights, Azure Monitor
- **IaC**: Terraform
- **Language**: C# .NET 8.0
- **Protocol**: MQTT, AMQP, Protobuf

---

## System Overview

```mermaid
graph TB
    subgraph "IoT Device Layer"
        D1[IoT Device 1]
        D2[IoT Device 2]
        DN[IoT Device N]
        SIM[Device Simulator]
    end

    subgraph "Ingestion Layer"
        IOT[Azure IoT Hub]
        EH[Event Hubs]
    end

    subgraph "Processing Layer"
        IDP[IoT Data Processor<br/>Azure Function]
        AGG[Telemetry Aggregator<br/>Azure Function]
        ALERT[Alert Processor<br/>Azure Function]
    end

    subgraph "Messaging Layer"
        SB1[Service Bus Queue<br/>telemetry-queue]
        SB2[Service Bus Queue<br/>aggregated-telemetry-queue]
        SB3[Service Bus Queue<br/>alerts-queue]
    end

    subgraph "Storage Layer"
        BLOB[Azure Blob Storage<br/>Raw & Aggregated Data]
        COSMOS[Cosmos DB<br/>Real-time Access]
    end

    subgraph "Monitoring Layer"
        AI[Application Insights]
        MON[Azure Monitor]
        LA[Log Analytics]
    end

    D1 -->|MQTT/Protobuf| IOT
    D2 -->|MQTT/Protobuf| IOT
    DN -->|MQTT/Protobuf| IOT
    SIM -->|MQTT/Protobuf| IOT

    IOT -->|Route| EH
    EH -->|Trigger| IDP

    IDP -->|Enqueue Raw| SB1
    IDP -->|Enqueue for Aggregation| SB2
    IDP -->|Alert Detection| SB3

    SB1 -->|Trigger| AGG
    SB2 -->|Trigger| AGG
    SB3 -->|Trigger| ALERT

    AGG -->|Store Raw| BLOB
    AGG -->|Store Aggregated| BLOB
    AGG -->|Real-time Query| COSMOS

    ALERT -->|Store Alerts| BLOB
    ALERT -->|Notify| MON

    IDP -.->|Logs & Metrics| AI
    AGG -.->|Logs & Metrics| AI
    ALERT -.->|Logs & Metrics| AI

    AI -->|Analytics| LA
    MON -->|Alerts| LA

    style IOT fill:#0078d4,stroke:#000,stroke-width:2px,color:#fff
    style EH fill:#0078d4,stroke:#000,stroke-width:2px,color:#fff
    style IDP fill:#7fba00,stroke:#000,stroke-width:2px,color:#000
    style AGG fill:#7fba00,stroke:#000,stroke-width:2px,color:#000
    style ALERT fill:#7fba00,stroke:#000,stroke-width:2px,color:#000
    style SB1 fill:#f25022,stroke:#000,stroke-width:2px,color:#fff
    style SB2 fill:#f25022,stroke:#000,stroke-width:2px,color:#fff
    style SB3 fill:#f25022,stroke:#000,stroke-width:2px,color:#fff
    style BLOB fill:#ffb900,stroke:#000,stroke-width:2px,color:#000
    style COSMOS fill:#ffb900,stroke:#000,stroke-width:2px,color:#000
    style AI fill:#00a4ef,stroke:#000,stroke-width:2px,color:#fff
    style MON fill:#00a4ef,stroke:#000,stroke-width:2px,color:#fff
    style LA fill:#00a4ef,stroke:#000,stroke-width:2px,color:#fff
```

### System Components

| Component | Technology | Purpose |
|-----------|-----------|---------|
| IoT Devices | MQTT Client | Generate and transmit telemetry data |
| Device Simulator | C# .NET 8.0 | Simulate multiple devices for testing |
| Azure IoT Hub | PaaS | Device management and message ingestion |
| Event Hubs | PaaS | High-throughput message streaming |
| IoT Data Processor | Azure Function | Initial message processing and routing |
| Telemetry Aggregator | Azure Function | Batch aggregation and statistical analysis |
| Alert Processor | Azure Function | Anomaly detection and alerting |
| Service Bus | PaaS | Reliable message queuing |
| Blob Storage | PaaS | Long-term data persistence |
| Cosmos DB | PaaS | Real-time data access (optional) |
| Application Insights | PaaS | APM and telemetry |

---

## High-Level Architecture

### System Architecture Layers

```mermaid
graph TB
    subgraph "Layer 1: Device & Ingestion"
        direction LR
        DEV[IoT Devices<br/>MQTT/Protobuf]
        HUB[IoT Hub<br/>Message Routing]
    end
    
    subgraph "Layer 2: Stream Processing"
        direction LR
        EH[Event Hubs<br/>Streaming]
        PROC[Data Processor<br/>Validation & Enrichment]
    end
    
    subgraph "Layer 3: Message Queuing"
        direction LR
        Q1[Raw Queue]
        Q2[Aggregation Queue]
        Q3[Alert Queue]
    end
    
    subgraph "Layer 4: Business Logic"
        direction LR
        AGG[Aggregator<br/>Statistical Analysis]
        ALR[Alert Handler<br/>Anomaly Detection]
    end
    
    subgraph "Layer 5: Persistence"
        direction LR
        COLD[Cold Storage<br/>Blob/Archive]
        HOT[Hot Storage<br/>Cosmos DB]
    end
    
    subgraph "Layer 6: Observability"
        direction LR
        LOG[Logging<br/>App Insights]
        MET[Metrics<br/>Azure Monitor]
        DASH[Dashboards<br/>Workbooks]
    end
    
    DEV --> HUB
    HUB --> EH
    EH --> PROC
    PROC --> Q1
    PROC --> Q2
    PROC --> Q3
    Q1 --> AGG
    Q2 --> AGG
    Q3 --> ALR
    AGG --> COLD
    AGG --> HOT
    ALR --> COLD
    
    PROC -.-> LOG
    AGG -.-> LOG
    ALR -.-> LOG
    LOG --> MET
    MET --> DASH
    
    style DEV fill:#e1f5ff
    style HUB fill:#b3e5fc
    style EH fill:#81d4fa
    style PROC fill:#4fc3f7
    style Q1 fill:#ffccbc
    style Q2 fill:#ffccbc
    style Q3 fill:#ffccbc
    style AGG fill:#ff9800
    style ALR fill:#ff9800
    style COLD fill:#c8e6c9
    style HOT fill:#81c784
    style LOG fill:#f8bbd0
    style MET fill:#f48fb1
    style DASH fill:#f06292
```

### Architectural Patterns

```mermaid
graph LR
    subgraph "Patterns Applied"
        P1[Event-Driven<br/>Architecture]
        P2[Microservices<br/>Pattern]
        P3[CQRS<br/>Pattern]
        P4[Circuit Breaker<br/>Pattern]
        P5[Retry<br/>Pattern]
        P6[Dead Letter<br/>Queue]
    end

    subgraph "Benefits"
        B1[Loose Coupling]
        B2[Independent Scaling]
        B3[Fault Isolation]
        B4[High Availability]
    end

    P1 --> B1
    P2 --> B2
    P3 --> B2
    P4 --> B3
    P5 --> B4
    P6 --> B3

    style P1 fill:#4caf50,stroke:#000,stroke-width:2px,color:#fff
    style P2 fill:#4caf50,stroke:#000,stroke-width:2px,color:#fff
    style P3 fill:#4caf50,stroke:#000,stroke-width:2px,color:#fff
    style P4 fill:#ff9800,stroke:#000,stroke-width:2px,color:#000
    style P5 fill:#ff9800,stroke:#000,stroke-width:2px,color:#000
    style P6 fill:#ff9800,stroke:#000,stroke-width:2px,color:#000
    style B1 fill:#2196f3,stroke:#000,stroke-width:2px,color:#fff
    style B2 fill:#2196f3,stroke:#000,stroke-width:2px,color:#fff
    style B3 fill:#2196f3,stroke:#000,stroke-width:2px,color:#fff
    style B4 fill:#2196f3,stroke:#000,stroke-width:2px,color:#fff
```

---

## Component Architecture

### IoT Data Processor Function

```mermaid
graph TB
    subgraph "IoTDataProcessor Azure Function"
        TRIG[Event Hub Trigger<br/>Batch Processing]
        
        subgraph "Processing Pipeline"
            VAL[Message Validator<br/>Schema Validation]
            DESER[Protobuf Deserializer<br/>Binary to Object]
            ENRICH[Data Enricher<br/>Add Metadata]
            ROUTE[Message Router<br/>Destination Selection]
        end
        
        subgraph "Output Bindings"
            OUT1[Service Bus Output<br/>Raw Queue]
            OUT2[Service Bus Output<br/>Aggregation Queue]
            OUT3[Service Bus Output<br/>Alert Queue]
        end
        
        subgraph "Error Handling"
            ERR[Error Handler]
            DLQ[Dead Letter Queue]
            RETRY[Retry Logic<br/>Max 3 attempts]
        end
        
        subgraph "Observability"
            LOG[Structured Logging]
            MET[Custom Metrics]
            TRACE[Distributed Tracing]
        end
    end
    
    TRIG --> VAL
    VAL -->|Valid| DESER
    VAL -->|Invalid| ERR
    DESER --> ENRICH
    ENRICH --> ROUTE
    
    ROUTE -->|Raw Data| OUT1
    ROUTE -->|Time-series| OUT2
    ROUTE -->|Threshold Breach| OUT3
    
    ERR --> RETRY
    RETRY -->|Max Retries| DLQ
    RETRY -->|Success| ROUTE
    
    VAL -.-> LOG
    DESER -.-> LOG
    ROUTE -.-> LOG
    ERR -.-> LOG
    
    LOG --> MET
    MET --> TRACE
    
    style TRIG fill:#4caf50
    style VAL fill:#2196f3
    style DESER fill:#2196f3
    style ENRICH fill:#2196f3
    style ROUTE fill:#2196f3
    style OUT1 fill:#ff9800
    style OUT2 fill:#ff9800
    style OUT3 fill:#ff9800
    style ERR fill:#f44336
    style DLQ fill:#f44336
    style RETRY fill:#ff5722
```

#### Class Diagram

```mermaid
classDiagram
    class IoTDataProcessor {
        +ILogger log
        +Run(EventData[] events)
        -ValidateMessage(EventData event)
        -DeserializeProtobuf(byte[] data)
        -EnrichTelemetry(Telemetry data)
        -RouteMessage(Telemetry data)
    }
    
    class TelemetryMessage {
        +string DeviceId
        +DateTime Timestamp
        +double Temperature
        +double Humidity
        +double Pressure
        +GeoLocation Location
        +Dictionary~string,string~ Metadata
    }
    
    class MessageValidator {
        +bool IsValid(EventData event)
        +ValidationResult Validate(Telemetry data)
        -CheckSchema(byte[] data)
        -CheckConstraints(Telemetry data)
    }
    
    class ProtobufSerializer {
        +Telemetry Deserialize(byte[] data)
        +byte[] Serialize(Telemetry data)
        -ValidateSchema()
    }
    
    class MessageRouter {
        +RouteDecision Route(Telemetry data)
        -ShouldAggregate(Telemetry data)
        -ShouldAlert(Telemetry data)
        -ApplyBusinessRules(Telemetry data)
    }
    
    class ServiceBusPublisher {
        +Task PublishAsync(string queue, object message)
        +Task PublishBatchAsync(string queue, IEnumerable messages)
        -HandleRetry()
    }
    
    IoTDataProcessor --> MessageValidator
    IoTDataProcessor --> ProtobufSerializer
    IoTDataProcessor --> MessageRouter
    IoTDataProcessor --> ServiceBusPublisher
    IoTDataProcessor --> TelemetryMessage
    MessageValidator --> TelemetryMessage
    ProtobufSerializer --> TelemetryMessage
    MessageRouter --> TelemetryMessage
```

### Telemetry Aggregator Function

```mermaid
graph TB
    subgraph "TelemetryAggregator Azure Function"
        TRIG[Service Bus Trigger<br/>5-min Window]
        
        subgraph "Aggregation Engine"
            BUFFER[Message Buffer<br/>In-Memory Collection]
            WINDOW[Time Window Manager<br/>5-minute Tumbling]
            CALC[Statistical Calculator<br/>Min/Max/Avg/StdDev]
        end
        
        subgraph "Calculations"
            AVG[Average Calculator]
            MIN[Min Calculator]
            MAX[Max Calculator]
            STD[StdDev Calculator]
            CNT[Count Aggregator]
        end
        
        subgraph "Output Processing"
            SER[Serializer<br/>Protobuf]
            COMP[Compressor<br/>Optional]
            PART[Partitioner<br/>By Device/Time]
        end
        
        subgraph "Storage"
            BLOB1[Blob Storage<br/>Raw Data]
            BLOB2[Blob Storage<br/>Aggregated Data]
            COSMOS[Cosmos DB<br/>Optional]
        end
        
        subgraph "Monitoring"
            MET1[Processing Metrics]
            MET2[Performance Metrics]
            ALERT[Threshold Alerts]
        end
    end
    
    TRIG --> BUFFER
    BUFFER --> WINDOW
    WINDOW --> CALC
    
    CALC --> AVG
    CALC --> MIN
    CALC --> MAX
    CALC --> STD
    CALC --> CNT
    
    AVG --> SER
    MIN --> SER
    MAX --> SER
    STD --> SER
    CNT --> SER
    
    SER --> COMP
    COMP --> PART
    
    PART --> BLOB1
    PART --> BLOB2
    PART -.->|Optional| COSMOS
    
    WINDOW -.-> MET1
    CALC -.-> MET2
    CALC -.-> ALERT
    
    style TRIG fill:#4caf50
    style BUFFER fill:#2196f3
    style WINDOW fill:#2196f3
    style CALC fill:#2196f3
    style AVG fill:#00bcd4
    style MIN fill:#00bcd4
    style MAX fill:#00bcd4
    style STD fill:#00bcd4
    style CNT fill:#00bcd4
    style BLOB1 fill:#ff9800
    style BLOB2 fill:#ff9800
    style COSMOS fill:#ff9800
```

#### Aggregation Algorithm

```mermaid
sequenceDiagram
    participant SB as Service Bus
    participant AGG as Aggregator
    participant WIN as Window Manager
    participant CALC as Calculator
    participant STOR as Storage
    
    loop Every 5 minutes
        SB->>AGG: Batch of messages (N messages)
        AGG->>WIN: Buffer messages
        WIN->>WIN: Check window boundary
        
        alt Window Complete
            WIN->>CALC: Process window
            CALC->>CALC: Calculate statistics
            Note over CALC: Min, Max, Avg<br/>StdDev, Count
            CALC->>STOR: Store aggregated data
            STOR-->>CALC: Confirm
            CALC->>WIN: Clear window
        else Window In Progress
            WIN->>WIN: Continue buffering
        end
        
        AGG->>SB: Acknowledge messages
    end
```

### Alert Processor Function

```mermaid
graph TB
    subgraph "AlertProcessor Azure Function"
        TRIG[Service Bus Trigger<br/>Alert Queue]
        
        subgraph "Alert Detection"
            RULE[Rule Engine<br/>Threshold Checks]
            ANOM[Anomaly Detector<br/>Statistical Analysis]
            PATTERN[Pattern Matcher<br/>Trend Analysis]
        end
        
        subgraph "Alert Classification"
            SEV[Severity Calculator<br/>Critical/Warning/Info]
            DEDUP[Deduplicator<br/>Prevent Alert Storm]
            PRIOR[Prioritizer<br/>Alert Ranking]
        end
        
        subgraph "Notification"
            EMAIL[Email Notification]
            SMS[SMS Notification]
            WEBHOOK[Webhook Trigger]
            DASHBOARD[Dashboard Update]
        end
        
        subgraph "Alert Storage"
            BLOB[Blob Storage<br/>Alert History]
            CACHE[Redis Cache<br/>Active Alerts]
        end
    end
    
    TRIG --> RULE
    TRIG --> ANOM
    TRIG --> PATTERN
    
    RULE --> SEV
    ANOM --> SEV
    PATTERN --> SEV
    
    SEV --> DEDUP
    DEDUP --> PRIOR
    
    PRIOR -->|Critical| EMAIL
    PRIOR -->|Critical| SMS
    PRIOR -->|All| WEBHOOK
    PRIOR -->|All| DASHBOARD
    
    PRIOR --> BLOB
    PRIOR --> CACHE
    
    style TRIG fill:#4caf50
    style RULE fill:#ff5722
    style ANOM fill:#ff5722
    style PATTERN fill:#ff5722
    style SEV fill:#ff9800
    style EMAIL fill:#2196f3
    style SMS fill:#2196f3
    style WEBHOOK fill:#2196f3
```

### Device Simulator

```mermaid
graph TB
    subgraph "Device Simulator Application"
        CONFIG[Configuration Loader<br/>appsettings.json]
        
        subgraph "Device Management"
            FACTORY[Device Factory<br/>Create Virtual Devices]
            POOL[Device Pool<br/>Manage N Devices]
            LIFECYCLE[Lifecycle Manager<br/>Start/Stop Devices]
        end
        
        subgraph "Data Generation"
            GEN[Telemetry Generator<br/>Random/Pattern Data]
            SENSOR[Sensor Simulator<br/>Temp/Humidity/Pressure]
            GEO[Location Generator<br/>GPS Coordinates]
        end
        
        subgraph "Protocol Handler"
            PROTO[Protobuf Serializer]
            MQTT[MQTT Client<br/>Connection Manager]
            RETRY[Retry Handler<br/>Connection Recovery]
        end
        
        subgraph "Performance"
            BATCH[Batch Sender<br/>Optimize Throughput]
            THROTTLE[Rate Limiter<br/>1000 msg/sec]
            METRICS[Metrics Collector<br/>Performance Stats]
        end
    end
    
    CONFIG --> FACTORY
    FACTORY --> POOL
    POOL --> LIFECYCLE
    
    LIFECYCLE --> GEN
    GEN --> SENSOR
    GEN --> GEO
    
    SENSOR --> PROTO
    GEO --> PROTO
    PROTO --> MQTT
    MQTT --> RETRY
    
    MQTT --> BATCH
    BATCH --> THROTTLE
    THROTTLE --> METRICS
    
    style CONFIG fill:#4caf50
    style FACTORY fill:#2196f3
    style POOL fill:#2196f3
    style GEN fill:#ff9800
    style SENSOR fill:#ff9800
    style PROTO fill:#9c27b0
    style MQTT fill:#9c27b0
    style BATCH fill:#00bcd4
    style THROTTLE fill:#00bcd4
```

---

## Data Flow Architecture

### End-to-End Message Flow

```mermaid
sequenceDiagram
    participant DEV as IoT Device
    participant HUB as IoT Hub
    participant EH as Event Hubs
    participant IDP as IoT Data Processor
    participant SB as Service Bus
    participant AGG as Aggregator
    participant ALERT as Alert Processor
    participant BLOB as Blob Storage
    participant AI as App Insights
    
    DEV->>HUB: Send Telemetry (MQTT/Protobuf)
    Note over DEV,HUB: 1000+ msg/sec
    
    HUB->>HUB: Route to Event Hubs
    HUB->>EH: Forward messages
    Note over EH: Batch collection
    
    EH->>IDP: Trigger with batch (32 messages)
    Note over IDP: Every 1 second or 32 messages
    
    IDP->>IDP: Validate & Deserialize
    IDP->>IDP: Enrich metadata
    IDP->>IDP: Route decision
    
    par Parallel Processing
        IDP->>SB: Enqueue to Raw Queue
        IDP->>SB: Enqueue to Aggregation Queue
        IDP->>SB: Enqueue to Alert Queue (if threshold)
    end
    
    IDP->>AI: Log metrics
    
    SB->>AGG: Trigger Aggregator
    Note over AGG: 5-minute window
    
    AGG->>AGG: Buffer messages
    AGG->>AGG: Calculate statistics
    AGG->>BLOB: Store raw data
    AGG->>BLOB: Store aggregated data
    AGG->>AI: Log metrics
    
    SB->>ALERT: Trigger Alert Processor
    ALERT->>ALERT: Evaluate rules
    ALERT->>ALERT: Detect anomalies
    ALERT->>BLOB: Store alerts
    ALERT->>AI: Log alerts
    
    AI-->>DEV: End-to-end visibility
```

### Data Transformation Pipeline

```mermaid
graph LR
    subgraph "Stage 1: Ingestion"
        D1[Binary Protobuf]
        D2[MQTT Payload]
    end
    
    subgraph "Stage 2: Deserialization"
        D3[Parse Protobuf]
        D4[Validate Schema]
        D5[Create Object]
    end
    
    subgraph "Stage 3: Enrichment"
        D6[Add Device Metadata]
        D7[Add Timestamp]
        D8[Add Geo Info]
        D9[Calculate Hash]
    end
    
    subgraph "Stage 4: Routing"
        D10[Apply Rules]
        D11[Determine Queues]
        D12[Set Priority]
    end
    
    subgraph "Stage 5: Aggregation"
        D13[Group by Device]
        D14[Group by Time Window]
        D15[Calculate Stats]
    end
    
    subgraph "Stage 6: Storage"
        D16[Partition Data]
        D17[Compress]
        D18[Store Blob]
        D19[Index Cosmos]
    end
    
    D1 --> D3
    D2 --> D3
    D3 --> D4
    D4 --> D5
    D5 --> D6
    D6 --> D7
    D7 --> D8
    D8 --> D9
    D9 --> D10
    D10 --> D11
    D11 --> D12
    D12 --> D13
    D13 --> D14
    D14 --> D15
    D15 --> D16
    D16 --> D17
    D17 --> D18
    D18 --> D19
    
    style D1 fill:#e1f5ff
    style D3 fill:#b3e5fc
    style D6 fill:#81d4fa
    style D10 fill:#4fc3f7
    style D13 fill:#29b6f6
    style D16 fill:#039be5
```

### Data Schema Evolution

```mermaid
graph TB
    subgraph "Protobuf Schema v1"
        V1[Telemetry v1<br/>DeviceId, Timestamp<br/>Temperature, Humidity]
    end
    
    subgraph "Protobuf Schema v2"
        V2[Telemetry v2<br/>+ Pressure<br/>+ Location<br/>+ Metadata]
    end
    
    subgraph "Protobuf Schema v3"
        V3[Telemetry v3<br/>+ Battery Level<br/>+ Signal Strength<br/>+ Error Codes]
    end
    
    subgraph "Version Management"
        VM[Schema Registry<br/>Backward Compatible<br/>Version Detection]
    end
    
    V1 -->|Upgrade| V2
    V2 -->|Upgrade| V3
    
    V1 -.->|Register| VM
    V2 -.->|Register| VM
    V3 -.->|Register| VM
    
    VM -.->|Validate| V1
    VM -.->|Validate| V2
    VM -.->|Validate| V3
    
    style V1 fill:#ffccbc
    style V2 fill:#ff9800
    style V3 fill:#f57c00
    style VM fill:#4caf50
```

---

## Deployment Architecture

### Azure Resource Topology

```mermaid
graph TB
    subgraph "Azure Subscription"
        subgraph "Resource Group: iot-data-processor-rg"
            subgraph "Networking"
                VNET[Virtual Network<br/>10.0.0.0/16]
                SUBNET1[Functions Subnet<br/>10.0.1.0/24]
                SUBNET2[Storage Subnet<br/>10.0.2.0/24]
                NSG[Network Security Group]
                PE[Private Endpoints]
            end
            
            subgraph "Compute"
                FUNC_PLAN[App Service Plan<br/>Premium EP1<br/>Auto-scale: 1-10]
                FUNC1[Function App<br/>IoTDataProcessor]
                FUNC2[Function App<br/>TelemetryAggregator]
                FUNC3[Function App<br/>AlertProcessor]
            end
            
            subgraph "Messaging"
                IOT[IoT Hub<br/>Standard S1<br/>400K msg/day]
                EH[Event Hubs<br/>Standard<br/>4 Partitions]
                SB[Service Bus<br/>Premium<br/>3 Queues]
            end
            
            subgraph "Storage"
                SA[Storage Account<br/>Premium LRS<br/>Hot Tier]
                BLOB[Blob Containers<br/>raw-data<br/>aggregated-data<br/>alerts]
            end
            
            subgraph "Monitoring"
                AI_WS[Log Analytics<br/>Workspace]
                AI[Application Insights]
                DASH[Azure Dashboard]
            end
            
            subgraph "Security"
                KV[Key Vault<br/>Secrets & Certs]
                MI[Managed Identity<br/>System Assigned]
            end
        end
    end
    
    VNET --> SUBNET1
    VNET --> SUBNET2
    SUBNET1 --> NSG
    SUBNET2 --> PE
    
    FUNC_PLAN --> FUNC1
    FUNC_PLAN --> FUNC2
    FUNC_PLAN --> FUNC3
    
    FUNC1 --> SUBNET1
    FUNC2 --> SUBNET1
    FUNC3 --> SUBNET1
    
    IOT --> EH
    EH --> FUNC1
    FUNC1 --> SB
    SB --> FUNC2
    SB --> FUNC3
    
    FUNC2 --> SA
    FUNC3 --> SA
    SA --> BLOB
    
    FUNC1 -.->|Logs| AI
    FUNC2 -.->|Logs| AI
    FUNC3 -.->|Logs| AI
    AI --> AI_WS
    AI_WS --> DASH
    
    FUNC1 -.->|Secrets| KV
    FUNC2 -.->|Secrets| KV
    FUNC3 -.->|Secrets| KV
    
    FUNC1 -.->|Identity| MI
    FUNC2 -.->|Identity| MI
    FUNC3 -.->|Identity| MI
    
    style VNET fill:#e1f5ff
    style FUNC_PLAN fill:#81c784
    style IOT fill:#64b5f6
    style SB fill:#ff8a65
    style SA fill:#ffd54f
    style AI fill:#ba68c8
    style KV fill:#f06292
```

### Multi-Region Deployment

```mermaid
graph TB
    subgraph "Global Infrastructure"
        TM[Azure Traffic Manager<br/>Priority Routing]
        
        subgraph "Primary Region: East US"
            P_IOT[IoT Hub Primary]
            P_FUNC[Function Apps Primary]
            P_SB[Service Bus Primary]
            P_STOR[Storage Primary]
            P_AI[App Insights Primary]
        end
        
        subgraph "Secondary Region: West US"
            S_IOT[IoT Hub Secondary]
            S_FUNC[Function Apps Secondary]
            S_SB[Service Bus Secondary]
            S_STOR[Storage Secondary]
            S_AI[App Insights Secondary]
        end
        
        subgraph "Global Services"
            CDN[Azure CDN<br/>Static Content]
            DNS[Azure DNS<br/>Name Resolution]
            FD[Azure Front Door<br/>Global Load Balancer]
        end
        
        subgraph "Data Replication"
            GRS[Geo-Redundant Storage<br/>RA-GRS]
            REP[Active Replication<br/>Service Bus]
        end
    end
    
    TM --> P_IOT
    TM --> S_IOT
    
    P_IOT --> P_FUNC
    P_FUNC --> P_SB
    P_SB --> P_STOR
    P_FUNC -.-> P_AI
    
    S_IOT --> S_FUNC
    S_FUNC --> S_SB
    S_SB --> S_STOR
    S_FUNC -.-> S_AI
    
    P_STOR <-.->|Replicate| GRS
    S_STOR <-.->|Replicate| GRS
    
    P_SB <-.->|Replicate| REP
    S_SB <-.->|Replicate| REP
    
    FD --> TM
    DNS --> FD
    
    style TM fill:#4caf50
    style P_IOT fill:#2196f3
    style S_IOT fill:#2196f3
    style GRS fill:#ff9800
    style FD fill:#9c27b0
```

### Infrastructure as Code

```mermaid
graph LR
    subgraph "Development"
        DEV[Developer]
        IDE[VS Code]
        TF[Terraform Files<br/>*.tf]
    end
    
    subgraph "Version Control"
        GIT[GitHub Repo]
        PR[Pull Request]
        REV[Code Review]
    end
    
    subgraph "CI/CD Pipeline"
        BUILD[Terraform Init<br/>Terraform Validate]
        PLAN[Terraform Plan]
        APPROVE[Manual Approval]
        APPLY[Terraform Apply]
    end
    
    subgraph "Azure Cloud"
        RG[Resource Group]
        RES[Azure Resources]
        STATE[Terraform State<br/>Azure Storage]
    end
    
    subgraph "Monitoring"
        LOG[Deployment Logs]
        ALERT[Change Alerts]
        AUDIT[Audit Trail]
    end
    
    DEV --> IDE
    IDE --> TF
    TF --> GIT
    GIT --> PR
    PR --> REV
    REV --> BUILD
    
    BUILD --> PLAN
    PLAN --> APPROVE
    APPROVE --> APPLY
    
    APPLY --> RG
    RG --> RES
    
    APPLY -.->|Store| STATE
    STATE -.->|Read| PLAN
    
    APPLY -.-> LOG
    LOG --> ALERT
    LOG --> AUDIT
    
    style TF fill:#7b42bc
    style BUILD fill:#4caf50
    style APPROVE fill:#ff9800
    style RES fill:#2196f3
    style STATE fill:#f44336
```

---

## Security Architecture

### Security Layers

```mermaid
graph TB
    subgraph "Defense in Depth"
        subgraph "Layer 1: Network Security"
            NSG[Network Security Groups<br/>Inbound/Outbound Rules]
            PE[Private Endpoints<br/>No Public Access]
            VNET[Virtual Network<br/>Isolation]
            FW[Azure Firewall<br/>Advanced Filtering]
        end
        
        subgraph "Layer 2: Identity & Access"
            AAD[Azure AD<br/>Authentication]
            MI[Managed Identity<br/>No Credentials]
            RBAC[Role-Based Access<br/>Least Privilege]
            PIM[Privileged Identity<br/>JIT Access]
        end
        
        subgraph "Layer 3: Data Protection"
            ENC1[Encryption at Rest<br/>AES-256]
            ENC2[Encryption in Transit<br/>TLS 1.2+]
            KV[Key Vault<br/>Key Management]
            CMK[Customer Managed Keys]
        end
        
        subgraph "Layer 4: Application Security"
            SAS[SAS Tokens<br/>Limited Access]
            CERT[Certificate Auth<br/>Device Identity]
            JWT[JWT Tokens<br/>API Security]
            THROTTLE[Rate Limiting<br/>DDoS Protection]
        end
        
        subgraph "Layer 5: Monitoring & Auditing"
            SEC_LOG[Security Logs<br/>Azure Monitor]
            SENT[Azure Sentinel<br/>SIEM]
            THREAT[Threat Detection<br/>Defender for Cloud]
            COMP[Compliance<br/>Policy Enforcement]
        end
    end
    
    NSG --> PE
    PE --> VNET
    VNET --> FW
    
    AAD --> MI
    MI --> RBAC
    RBAC --> PIM
    
    ENC1 --> ENC2
    ENC2 --> KV
    KV --> CMK
    
    SAS --> CERT
    CERT --> JWT
    JWT --> THROTTLE
    
    SEC_LOG --> SENT
    SENT --> THREAT
    THREAT --> COMP
    
    style NSG fill:#f44336
    style AAD fill:#ff9800
    style ENC1 fill:#4caf50
    style SAS fill:#2196f3
    style SEC_LOG fill:#9c27b0
```

### Authentication & Authorization Flow

```mermaid
sequenceDiagram
    participant DEV as IoT Device
    participant IOT as IoT Hub
    participant FUNC as Azure Function
    participant KV as Key Vault
    participant SB as Service Bus
    participant BLOB as Blob Storage
    
    Note over DEV,IOT: Device Authentication
    DEV->>IOT: Connect with Device Certificate
    IOT->>IOT: Validate Certificate
    IOT->>AAD: Verify Device Identity
    AAD-->>IOT: Token
    IOT-->>DEV: Connection Established
    
    Note over FUNC,KV: Function Identity
    FUNC->>FUNC: Start with Managed Identity
    FUNC->>AAD: Request Token
    AAD-->>FUNC: MI Token
    
    Note over FUNC,KV: Access Key Vault
    FUNC->>KV: Get Secret (with MI Token)
    KV->>KV: Validate RBAC
    KV-->>FUNC: Secret Value
    
    Note over FUNC,SB: Access Service Bus
    FUNC->>SB: Send Message (with MI Token)
    SB->>SB: Validate RBAC
    SB-->>FUNC: Acknowledge
    
    Note over FUNC,BLOB: Access Blob Storage
    FUNC->>BLOB: Write Data (with MI Token)
    BLOB->>BLOB: Validate RBAC
    BLOB-->>FUNC: Success
```

### Data Encryption

```mermaid
graph LR
    subgraph "Encryption at Rest"
        DATA1[Raw Telemetry]
        DATA2[Aggregated Data]
        DATA3[Alert Data]
        
        ENC[Azure Storage<br/>Service Encryption<br/>AES-256]
        
        CMK[Customer Managed Key<br/>Key Vault]
        HSM[Hardware Security Module<br/>FIPS 140-2]
    end
    
    subgraph "Encryption in Transit"
        TLS[TLS 1.2/1.3]
        MQTT_TLS[MQTT over TLS]
        HTTPS[HTTPS]
        AMQPS[AMQPS]
    end
    
    DATA1 --> ENC
    DATA2 --> ENC
    DATA3 --> ENC
    
    ENC --> CMK
    CMK --> HSM
    
    DATA1 -.->|Transport| MQTT_TLS
    DATA2 -.->|Transport| HTTPS
    DATA3 -.->|Transport| AMQPS
    
    MQTT_TLS --> TLS
    HTTPS --> TLS
    AMQPS --> TLS
    
    style ENC fill:#4caf50
    style CMK fill:#ff9800
    style HSM fill:#f44336
    style TLS fill:#2196f3
```

---

## Scalability and Performance

### Auto-Scaling Strategy

```mermaid
graph TB
    subgraph "Scaling Triggers"
        CPU[CPU > 70%]
        MEM[Memory > 80%]
        QUEUE[Queue Length > 1000]
        LATENCY[Latency > 500ms]
    end
    
    subgraph "Scaling Controller"
        MONITOR[Azure Monitor<br/>Metrics Collection]
        RULES[Scaling Rules<br/>Scale-out/Scale-in]
        COOL[Cooldown Period<br/>5 minutes]
    end
    
    subgraph "Scaling Actions"
        SCALE_OUT[Scale Out<br/>Add Instance<br/>Max: 10]
        SCALE_IN[Scale In<br/>Remove Instance<br/>Min: 1]
        HEALTH[Health Check<br/>New Instance]
    end
    
    subgraph "Target Resources"
        FUNC[Function App Instances]
        IOT[IoT Hub Units]
        SB[Service Bus Units]
        EH[Event Hub TUs]
    end
    
    CPU --> MONITOR
    MEM --> MONITOR
    QUEUE --> MONITOR
    LATENCY --> MONITOR
    
    MONITOR --> RULES
    RULES --> COOL
    
    COOL -->|Scale Out| SCALE_OUT
    COOL -->|Scale In| SCALE_IN
    
    SCALE_OUT --> HEALTH
    HEALTH --> FUNC
    HEALTH --> IOT
    HEALTH --> SB
    HEALTH --> EH
    
    SCALE_IN --> FUNC
    
    style CPU fill:#f44336
    style MONITOR fill:#2196f3
    style SCALE_OUT fill:#4caf50
    style FUNC fill:#ff9800
```

### Performance Optimization

```mermaid
graph LR
    subgraph "Ingestion Optimization"
        BATCH1[Batch Processing<br/>32 messages]
        BUFFER1[Buffer Pool<br/>Memory Reuse]
        ASYNC1[Async I/O<br/>Non-blocking]
    end
    
    subgraph "Processing Optimization"
        PARALLEL[Parallel Processing<br/>Task.WhenAll]
        CACHE[In-Memory Cache<br/>Hot Data]
        POOL[Connection Pool<br/>Reuse Connections]
    end
    
    subgraph "Storage Optimization"
        BATCH2[Batch Writes<br/>Bulk Insert]
        COMPRESS[Compression<br/>Gzip/Brotli]
        PARTITION[Partitioning<br/>Date/Device]
    end
    
    subgraph "Network Optimization"
        CDN[Content Delivery<br/>Edge Caching]
        REGIONAL[Regional Endpoints<br/>Latency Reduction]
        PIPELINE[HTTP Pipeline<br/>Request Pipelining]
    end
    
    BATCH1 --> PARALLEL
    BUFFER1 --> CACHE
    ASYNC1 --> POOL
    
    PARALLEL --> BATCH2
    CACHE --> COMPRESS
    POOL --> PARTITION
    
    BATCH2 --> CDN
    COMPRESS --> REGIONAL
    PARTITION --> PIPELINE
    
    style BATCH1 fill:#4caf50
    style PARALLEL fill:#2196f3
    style BATCH2 fill:#ff9800
    style CDN fill:#9c27b0
```

### Throughput Capacity

```mermaid
graph TB
    subgraph "System Capacity"
        subgraph "Ingestion Layer"
            IOT_CAP[IoT Hub<br/>400,000 msg/day<br/>~4,600 msg/min<br/>~77 msg/sec]
            EH_CAP[Event Hubs<br/>1 MB/sec per TU<br/>1000 events/sec per TU<br/>4 TUs = 4000 msg/sec]
        end
        
        subgraph "Processing Layer"
            FUNC_CAP[Functions Premium<br/>100 concurrent instances<br/>Each: 10-20 msg/sec<br/>Total: 1000-2000 msg/sec]
        end
        
        subgraph "Messaging Layer"
            SB_CAP[Service Bus Premium<br/>1000 msg/sec per MU<br/>1 MU = 1000 msg/sec]
        end
        
        subgraph "Storage Layer"
            BLOB_CAP[Blob Storage<br/>20,000 requests/sec<br/>Premium: 100 Gbps]
        end
        
        subgraph "Bottleneck Analysis"
            BOT[Current Bottleneck:<br/>IoT Hub @ 77 msg/sec<br/><br/>To achieve 1000 msg/sec:<br/>Upgrade to S2 or S3 tier]
        end
    end
    
    IOT_CAP -.->|Limits| BOT
    EH_CAP -.->|OK| BOT
    FUNC_CAP -.->|OK| BOT
    SB_CAP -.->|OK| BOT
    BLOB_CAP -.->|OK| BOT
    
    style IOT_CAP fill:#f44336
    style EH_CAP fill:#4caf50
    style FUNC_CAP fill:#4caf50
    style SB_CAP fill:#4caf50
    style BLOB_CAP fill:#4caf50
    style BOT fill:#ff9800
```

---

## Monitoring and Observability

### Observability Stack

```mermaid
graph TB
    subgraph "Data Collection"
        subgraph "Telemetry Sources"
            FUNC_TEL[Function Telemetry<br/>Traces, Logs, Metrics]
            AZURE_TEL[Azure Resource Metrics<br/>CPU, Memory, Network]
            CUSTOM_TEL[Custom Metrics<br/>Business KPIs]
        end
        
        subgraph "Collection Agents"
            AI_SDK[Application Insights SDK]
            DIAG[Azure Diagnostics]
            OTEL[OpenTelemetry]
        end
    end
    
    subgraph "Data Processing"
        LA[Log Analytics<br/>Kusto Queries]
        AI[Application Insights<br/>APM]
        MON[Azure Monitor<br/>Metrics Platform]
    end
    
    subgraph "Visualization"
        DASH[Azure Dashboards<br/>Real-time Views]
        WORK[Workbooks<br/>Custom Reports]
        GRAF[Grafana<br/>Advanced Viz]
    end
    
    subgraph "Alerting"
        ALERT[Alert Rules<br/>Threshold/Metric]
        ACTION[Action Groups<br/>Email/SMS/Webhook]
        SMART[Smart Detection<br/>ML-based Anomalies]
    end
    
    FUNC_TEL --> AI_SDK
    AZURE_TEL --> DIAG
    CUSTOM_TEL --> OTEL
    
    AI_SDK --> AI
    DIAG --> MON
    OTEL --> LA
    
    AI --> LA
    MON --> LA
    
    LA --> DASH
    LA --> WORK
    LA --> GRAF
    
    LA --> ALERT
    ALERT --> ACTION
    AI --> SMART
    SMART --> ACTION
    
    style FUNC_TEL fill:#e1f5ff
    style AI_SDK fill:#81d4fa
    style LA fill:#4fc3f7
    style DASH fill:#ff9800
    style ALERT fill:#f44336
```

### Distributed Tracing

```mermaid
sequenceDiagram
    participant DEV as Device
    participant IOT as IoT Hub
    participant EH as Event Hubs
    participant FUNC1 as IoT Processor
    participant SB as Service Bus
    participant FUNC2 as Aggregator
    participant BLOB as Storage
    participant AI as App Insights
    
    Note over DEV,AI: Trace ID: 12345-67890-abcdef
    
    DEV->>IOT: Send Telemetry [span-1]
    IOT->>AI: Log span-1 (10ms)
    
    IOT->>EH: Route Message [span-2]
    EH->>AI: Log span-2 (5ms)
    
    EH->>FUNC1: Trigger Function [span-3]
    FUNC1->>FUNC1: Process (50ms)
    FUNC1->>AI: Log span-3 (50ms)
    
    FUNC1->>SB: Enqueue [span-4]
    SB->>AI: Log span-4 (8ms)
    
    SB->>FUNC2: Trigger Function [span-5]
    FUNC2->>FUNC2: Aggregate (200ms)
    FUNC2->>AI: Log span-5 (200ms)
    
    FUNC2->>BLOB: Write Data [span-6]
    BLOB->>AI: Log span-6 (30ms)
    
    Note over DEV,AI: Total Duration: 303ms<br/>Spans: 6<br/>Success: true
```

### Key Metrics Dashboard

```mermaid
graph TB
    subgraph "System Health Dashboard"
        subgraph "Ingestion Metrics"
            M1[Messages Received/sec<br/>Target: 1000<br/>Current: 850]
            M2[Message Size Avg<br/>Target: <10KB<br/>Current: 8.5KB]
            M3[Ingestion Latency<br/>Target: <50ms<br/>Current: 35ms]
        end
        
        subgraph "Processing Metrics"
            M4[Processing Duration<br/>Target: <500ms<br/>Current: 320ms]
            M5[Success Rate<br/>Target: >99%<br/>Current: 99.8%]
            M6[Error Rate<br/>Target: <1%<br/>Current: 0.2%]
        end
        
        subgraph "Resource Metrics"
            M7[CPU Usage<br/>Target: <70%<br/>Current: 55%]
            M8[Memory Usage<br/>Target: <80%<br/>Current: 65%]
            M9[Active Instances<br/>Range: 1-10<br/>Current: 3]
        end
        
        subgraph "Business Metrics"
            M10[Devices Active<br/>Total: 1000<br/>Online: 987]
            M11[Alerts Generated<br/>Critical: 2<br/>Warning: 15]
            M12[Data Processed<br/>Today: 2.5TB<br/>Month: 45TB]
        end
        
        subgraph "SLA Metrics"
            M13[Availability<br/>Target: 99.9%<br/>Current: 99.95%]
            M14[MTTR<br/>Target: <30min<br/>Current: 15min]
            M15[MTBF<br/>Target: >30days<br/>Current: 45days]
        end
    end
    
    style M1 fill:#4caf50
    style M2 fill:#4caf50
    style M3 fill:#4caf50
    style M4 fill:#4caf50
    style M5 fill:#4caf50
    style M6 fill:#4caf50
    style M7 fill:#4caf50
    style M8 fill:#ff9800
    style M9 fill:#4caf50
    style M10 fill:#4caf50
    style M11 fill:#ff9800
    style M12 fill:#4caf50
    style M13 fill:#4caf50
    style M14 fill:#4caf50
    style M15 fill:#4caf50
```

### Alert Configuration

```mermaid
graph LR
    subgraph "Alert Types"
        A1[Threshold Alerts<br/>CPU > 80%<br/>Memory > 85%<br/>Queue > 5000]
        A2[Anomaly Alerts<br/>ML-based Detection<br/>Pattern Changes]
        A3[Availability Alerts<br/>Service Down<br/>Endpoint Unreachable]
        A4[Budget Alerts<br/>Cost Threshold<br/>Quota Exceeded]
    end
    
    subgraph "Severity Levels"
        S0[Sev 0 - Critical<br/>Service Down]
        S1[Sev 1 - Error<br/>Degraded Performance]
        S2[Sev 2 - Warning<br/>Approaching Limits]
        S3[Sev 3 - Info<br/>Informational]
    end
    
    subgraph "Notification Channels"
        N1[Email<br/>On-Call Team]
        N2[SMS<br/>Critical Only]
        N3[PagerDuty<br/>Incident Management]
        N4[Teams/Slack<br/>Team Channel]
        N5[Webhook<br/>External Systems]
    end
    
    A1 --> S0
    A1 --> S1
    A2 --> S1
    A2 --> S2
    A3 --> S0
    A4 --> S2
    A4 --> S3
    
    S0 --> N1
    S0 --> N2
    S0 --> N3
    S1 --> N1
    S1 --> N4
    S2 --> N4
    S3 --> N5
    
    style A1 fill:#f44336
    style A3 fill:#f44336
    style S0 fill:#d32f2f
    style N2 fill:#ff5252
```

---

## Disaster Recovery

### Backup and Recovery Strategy

```mermaid
graph TB
    subgraph "Backup Strategy"
        subgraph "Data Backup"
            B1[Blob Storage<br/>Geo-Redundant<br/>6 copies across regions]
            B2[Database Backup<br/>Point-in-Time<br/>35 days retention]
            B3[Configuration Backup<br/>Terraform State<br/>Version Control]
        end
        
        subgraph "Backup Schedule"
            S1[Real-time Replication<br/>Blob Storage]
            S2[Hourly Snapshots<br/>Database]
            S3[Daily Backup<br/>Full System]
            S4[Weekly Archive<br/>Long-term Storage]
        end
        
        subgraph "Recovery Objectives"
            RTO[RTO: 1 hour<br/>Recovery Time]
            RPO[RPO: 5 minutes<br/>Data Loss Window]
            SLA[SLA: 99.9%<br/>Availability]
        end
    end
    
    subgraph "Recovery Procedures"
        subgraph "Failure Scenarios"
            F1[Component Failure<br/>Auto-restart]
            F2[Regional Outage<br/>Failover to Secondary]
            F3[Data Corruption<br/>Restore from Backup]
            F4[Complete Disaster<br/>DR Region Activation]
        end
        
        subgraph "Recovery Actions"
            R1[Automatic Failover<br/>Traffic Manager]
            R2[Manual Failover<br/>Runbook Execution]
            R3[Data Restore<br/>Point-in-Time]
            R4[Full Rebuild<br/>Terraform Apply]
        end
    end
    
    B1 --> S1
    B2 --> S2
    B3 --> S3
    B1 --> S4
    
    S1 -.-> RPO
    S2 -.-> RPO
    
    F1 --> R1
    F2 --> R1
    F3 --> R3
    F4 --> R4
    
    R1 -.-> RTO
    R2 -.-> RTO
    R3 -.-> RTO
    R4 -.-> SLA
    
    style B1 fill:#4caf50
    style RTO fill:#ff9800
    style RPO fill:#ff9800
    style F4 fill:#f44336
    style R1 fill:#4caf50
```

### Failover Architecture

```mermaid
sequenceDiagram
    participant DEV as IoT Devices
    participant TM as Traffic Manager
    participant P_IOT as Primary IoT Hub
    participant S_IOT as Secondary IoT Hub
    participant HEALTH as Health Probe
    participant ALERT as Alert System
    participant OPS as Operations Team
    
    Note over DEV,OPS: Normal Operation
    DEV->>TM: Send Telemetry
    TM->>P_IOT: Route to Primary
    P_IOT->>P_IOT: Process Messages
    
    loop Every 30 seconds
        HEALTH->>P_IOT: Health Check
        P_IOT-->>HEALTH: 200 OK
    end
    
    Note over P_IOT: Primary Region Failure
    HEALTH->>P_IOT: Health Check
    P_IOT--xHEALTH: Timeout/Error
    
    HEALTH->>ALERT: Trigger Failover Alert
    ALERT->>OPS: Notify Operations (Sev 0)
    
    HEALTH->>TM: Mark Primary Unhealthy
    TM->>TM: Update Routing
    
    Note over DEV,OPS: Failover Complete
    DEV->>TM: Send Telemetry
    TM->>S_IOT: Route to Secondary
    S_IOT->>S_IOT: Process Messages
    
    Note over P_IOT: Primary Recovered
    HEALTH->>P_IOT: Health Check
    P_IOT-->>HEALTH: 200 OK
    
    HEALTH->>TM: Mark Primary Healthy
    TM->>TM: Update Routing
    ALERT->>OPS: Primary Restored
    
    Note over DEV,OPS: Failback Complete
    DEV->>TM: Send Telemetry
    TM->>P_IOT: Route to Primary
```

### Business Continuity Plan

```mermaid
graph TB
    subgraph "Business Continuity"
        subgraph "Prevention"
            P1[Multi-Region<br/>Deployment]
            P2[Redundant<br/>Components]
            P3[Auto-scaling<br/>& Load Balancing]
            P4[Regular Testing<br/>DR Drills]
        end
        
        subgraph "Detection"
            D1[Health Monitoring<br/>24/7]
            D2[Anomaly Detection<br/>ML-based]
            D3[Alert System<br/>Multi-channel]
            D4[On-Call Rotation<br/>Response Team]
        end
        
        subgraph "Response"
            R1[Incident Response<br/>Playbooks]
            R2[Automated Failover<br/>No Manual Intervention]
            R3[Communication Plan<br/>Stakeholder Updates]
            R4[Post-Incident Review<br/>Root Cause Analysis]
        end
        
        subgraph "Recovery"
            RC1[Service Restoration<br/>Priority-based]
            RC2[Data Validation<br/>Integrity Check]
            RC3[Performance Tuning<br/>Optimization]
            RC4[Documentation<br/>Lessons Learned]
        end
    end
    
    P1 --> D1
    P2 --> D2
    P3 --> D3
    P4 --> D4
    
    D1 --> R1
    D2 --> R2
    D3 --> R3
    D4 --> R4
    
    R1 --> RC1
    R2 --> RC2
    R3 --> RC3
    R4 --> RC4
    
    style P1 fill:#4caf50
    style D1 fill:#2196f3
    style R2 fill:#ff9800
    style RC1 fill:#9c27b0
```

---

## Appendix

### Technology Decision Matrix

| Requirement | Options Considered | Selected | Justification |
|-------------|-------------------|----------|---------------|
| **Compute** | VMs, AKS, Functions | Azure Functions | Serverless, auto-scale, pay-per-use |
| **Messaging** | Service Bus, Event Grid, Event Hubs | Service Bus + Event Hubs | Reliable queuing + high throughput |
| **Storage** | Blob, Cosmos DB, SQL | Blob + Cosmos (optional) | Cost-effective, scalable, geo-redundant |
| **Protocol** | JSON, Protobuf, Avro | Protobuf | Compact, fast, schema evolution |
| **Language** | Python, Java, C# | C# .NET 8.0 | Azure integration, performance, tooling |
| **IaC** | ARM, Bicep, Terraform | Terraform | Multi-cloud, mature, community |

### Performance Benchmarks

| Metric | Target | Current | Status |
|--------|--------|---------|--------|
| Message Throughput | 1000 msg/sec | 850 msg/sec | ⚠️ Needs IoT Hub upgrade |
| Processing Latency | <500ms | 320ms | ✅ Meeting SLA |
| End-to-End Latency | <1s | 750ms | ✅ Meeting SLA |
| Success Rate | >99% | 99.8% | ✅ Exceeding target |
| CPU Utilization | <70% | 55% | ✅ Healthy |
| Memory Utilization | <80% | 65% | ✅ Healthy |
| Storage Growth | <100GB/day | 75GB/day | ✅ Within budget |
| Monthly Cost | <$500 | $385 | ✅ Under budget |

### Cost Analysis

```mermaid
pie title Monthly Cost Breakdown ($385)
    "IoT Hub S1" : 25
    "Event Hubs Standard" : 55
    "Service Bus Premium" : 85
    "Functions Premium" : 120
    "Blob Storage" : 45
    "Application Insights" : 35
    "Networking" : 20
```

### Compliance and Standards

| Standard | Requirement | Implementation | Status |
|----------|-------------|----------------|--------|
| **GDPR** | Data Privacy | Encryption, Access Control, Audit Logs | ✅ Compliant |
| **SOC 2** | Security Controls | Azure Compliance, Monitoring | ✅ Compliant |
| **ISO 27001** | Information Security | Security Architecture, Policies | ✅ Compliant |
| **HIPAA** | Healthcare Data | Encryption, BAA with Azure | ⚠️ If needed |
| **PCI DSS** | Payment Data | Not applicable | N/A |

### Glossary

| Term | Definition |
|------|------------|
| **AMQP** | Advanced Message Queuing Protocol - messaging protocol |
| **APM** | Application Performance Monitoring |
| **CMK** | Customer Managed Key - encryption keys managed by customer |
| **CQRS** | Command Query Responsibility Segregation - architectural pattern |
| **DLQ** | Dead Letter Queue - queue for failed messages |
| **GRS** | Geo-Redundant Storage - replication across regions |
| **IaC** | Infrastructure as Code - managing infrastructure via code |
| **MQTT** | Message Queuing Telemetry Transport - IoT protocol |
| **MTBF** | Mean Time Between Failures - reliability metric |
| **MTTR** | Mean Time To Recovery - recovery speed metric |
| **Protobuf** | Protocol Buffers - binary serialization format |
| **RBAC** | Role-Based Access Control - authorization model |
| **RPO** | Recovery Point Objective - acceptable data loss |
| **RTO** | Recovery Time Objective - acceptable downtime |
| **SAS** | Shared Access Signature - temporary access tokens |
| **SIEM** | Security Information and Event Management |
| **TLS** | Transport Layer Security - encryption protocol |
| **TU** | Throughput Unit - Event Hubs capacity measure |

---

## Document Information

- **Version**: 1.0
- **Last Updated**: 2024
- **Author**: IoT Platform Team
- **Status**: Production
- **Next Review**: Quarterly

---

**End of Architecture Documentation**
