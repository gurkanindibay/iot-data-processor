using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IoTDataProcessor.Tests
{
    public class TelemetryProcessorTests
    {
        private readonly Mock<ILogger<TelemetryAggregator>> _mockLogger;

        public TelemetryProcessorTests()
        {
            _mockLogger = new Mock<ILogger<TelemetryAggregator>>();
        }

        [Fact]
        public void TelemetryAggregator_CanBeInstantiated()
        {
            // Arrange & Act
            var aggregator = new TelemetryAggregator(_mockLogger.Object);

            // Assert
            Assert.NotNull(aggregator);
        }

        [Fact]
        public void CreateTelemetryMessage_ShouldReturnValidProtobufMessage()
        {
            // Arrange
            var telemetry = new Iotdataprocessor.Telemetry
            {
                SensorId = "sensor-001",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Value = 25.5,
                Unit = "celsius"
            };

            // Act
            var bytes = telemetry.ToByteArray();
            var deserializedTelemetry = Iotdataprocessor.Telemetry.Parser.ParseFrom(bytes);

            // Assert
            Assert.Equal("sensor-001", deserializedTelemetry.SensorId);
            Assert.Equal(25.5, deserializedTelemetry.Value);
            Assert.Equal("celsius", deserializedTelemetry.Unit);
        }

        [Theory]
        [InlineData("sensor-001", 20.0, "celsius")]
        [InlineData("sensor-002", 75.5, "fahrenheit")]
        [InlineData("sensor-003", -10.0, "celsius")]
        public void Telemetry_ShouldSerializeAndDeserialize_WithDifferentValues(
            string sensorId, double value, string unit)
        {
            // Arrange
            var telemetry = new Iotdataprocessor.Telemetry
            {
                SensorId = sensorId,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Value = value,
                Unit = unit
            };

            // Act
            var bytes = telemetry.ToByteArray();
            var deserializedTelemetry = Iotdataprocessor.Telemetry.Parser.ParseFrom(bytes);

            // Assert
            Assert.Equal(sensorId, deserializedTelemetry.SensorId);
            Assert.Equal(value, deserializedTelemetry.Value);
            Assert.Equal(unit, deserializedTelemetry.Unit);
        }

        [Fact]
        public void Telemetry_ShouldHaveCorrectTimestamp()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;
            var expectedTimestamp = now.ToUnixTimeMilliseconds();
            
            var telemetry = new Iotdataprocessor.Telemetry
            {
                SensorId = "sensor-001",
                Timestamp = expectedTimestamp,
                Value = 25.5,
                Unit = "celsius"
            };

            // Act
            var actualTimestamp = telemetry.Timestamp;
            var actualDateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(actualTimestamp);

            // Assert
            Assert.Equal(expectedTimestamp, actualTimestamp);
            Assert.True(Math.Abs((now - actualDateTimeOffset).TotalSeconds) < 1);
        }

        [Fact]
        public void ProtobufSerialization_ShouldBeMoreCompactThanJson()
        {
            // Arrange
            var telemetry = new Iotdataprocessor.Telemetry
            {
                SensorId = "sensor-001",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Value = 25.5,
                Unit = "celsius"
            };

            // Act
            var protobufBytes = telemetry.ToByteArray();
            var jsonString = System.Text.Json.JsonSerializer.Serialize(new
            {
                sensorId = telemetry.SensorId,
                timestamp = telemetry.Timestamp,
                value = telemetry.Value,
                unit = telemetry.Unit
            });
            var jsonBytes = Encoding.UTF8.GetBytes(jsonString);

            // Assert
            Assert.True(protobufBytes.Length < jsonBytes.Length, 
                $"Protobuf ({protobufBytes.Length} bytes) should be smaller than JSON ({jsonBytes.Length} bytes)");
        }

        [Fact]
        public void TelemetryAggregate_ShouldStoreMultipleValues()
        {
            // Arrange
            var aggregate = new Iotdataprocessor.TelemetryAggregate
            {
                SensorId = "sensor-001",
                WindowStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                WindowEnd = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds(),
                AvgValue = 25.5,
                MinValue = 20.0,
                MaxValue = 30.0,
                Count = 100,
                Unit = "celsius"
            };

            // Act
            var bytes = aggregate.ToByteArray();
            var deserialized = Iotdataprocessor.TelemetryAggregate.Parser.ParseFrom(bytes);

            // Assert
            Assert.Equal("sensor-001", deserialized.SensorId);
            Assert.Equal(25.5, deserialized.AvgValue);
            Assert.Equal(20.0, deserialized.MinValue);
            Assert.Equal(30.0, deserialized.MaxValue);
            Assert.Equal(100, deserialized.Count);
        }

        [Fact]
        public void AnomalyDetector_CanBeInstantiated()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<AnomalyDetector>>();

            // Act
            var detector = new AnomalyDetector(mockLogger.Object);

            // Assert
            Assert.NotNull(detector);
        }
    }
}
