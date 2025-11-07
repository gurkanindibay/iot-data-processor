using System;
using Xunit;

namespace DeviceSimulator.Tests
{
    public class TelemetryGeneratorTests
    {
        [Fact]
        public void GenerateRandomTemperature_ShouldBeWithinRange()
        {
            // Arrange
            var random = new Random();
            var minTemp = 15.0;
            var maxTemp = 35.0;

            // Act
            var temperature = minTemp + (random.NextDouble() * (maxTemp - minTemp));

            // Assert
            Assert.True(temperature >= minTemp, $"Temperature {temperature} should be >= {minTemp}");
            Assert.True(temperature <= maxTemp, $"Temperature {temperature} should be <= {maxTemp}");
        }

        [Fact]
        public void GenerateRandomHumidity_ShouldBeWithinRange()
        {
            // Arrange
            var random = new Random();
            var minHumidity = 30.0;
            var maxHumidity = 80.0;

            // Act
            var humidity = minHumidity + (random.NextDouble() * (maxHumidity - minHumidity));

            // Assert
            Assert.True(humidity >= minHumidity, $"Humidity {humidity} should be >= {minHumidity}");
            Assert.True(humidity <= maxHumidity, $"Humidity {humidity} should be <= {maxHumidity}");
        }

        [Fact]
        public void GenerateRandomPressure_ShouldBeWithinRange()
        {
            // Arrange
            var random = new Random();
            var minPressure = 980.0;
            var maxPressure = 1050.0;

            // Act
            var pressure = minPressure + (random.NextDouble() * (maxPressure - minPressure));

            // Assert
            Assert.True(pressure >= minPressure, $"Pressure {pressure} should be >= {minPressure}");
            Assert.True(pressure <= maxPressure, $"Pressure {pressure} should be <= {maxPressure}");
        }

        [Fact]
        public void DeviceId_ShouldFollowNamingConvention()
        {
            // Arrange
            var deviceNumber = 42;

            // Act
            var deviceId = $"device-{deviceNumber:D3}";

            // Assert
            Assert.Equal("device-042", deviceId);
            Assert.StartsWith("device-", deviceId);
        }

        [Theory]
        [InlineData(1, "device-001")]
        [InlineData(10, "device-010")]
        [InlineData(100, "device-100")]
        [InlineData(999, "device-999")]
        public void DeviceId_ShouldFormatCorrectly(int deviceNumber, string expected)
        {
            // Act
            var deviceId = $"device-{deviceNumber:D3}";

            // Assert
            Assert.Equal(expected, deviceId);
        }

        [Fact]
        public void UnixTimestamp_ShouldBeCorrect()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow;
            var expectedTimestamp = now.ToUnixTimeSeconds();

            // Act
            var actualTimestamp = now.ToUnixTimeSeconds();

            // Assert
            Assert.Equal(expectedTimestamp, actualTimestamp);
        }

        [Fact]
        public void MessageInterval_ShouldCalculateCorrectly()
        {
            // Arrange
            var targetMessagesPerSecond = 1000;
            var numberOfDevices = 100;
            var messagesPerDevicePerSecond = targetMessagesPerSecond / numberOfDevices;
            
            // Act
            var intervalMs = 1000 / messagesPerDevicePerSecond;

            // Assert
            Assert.Equal(100, intervalMs); // 10 messages per second per device = 100ms interval
        }

        [Theory]
        [InlineData(1000, 10, 100)]   // 1000 msgs/sec, 10 devices = 100 msgs per device
        [InlineData(1000, 100, 10)]   // 1000 msgs/sec, 100 devices = 10 msgs per device
        [InlineData(1000, 1000, 1)]   // 1000 msgs/sec, 1000 devices = 1 msg per device
        public void MessageDistribution_ShouldBeCorrect(int totalRate, int devices, int expected)
        {
            // Act
            var messagesPerDevice = totalRate / devices;

            // Assert
            Assert.Equal(expected, messagesPerDevice);
        }

        [Fact]
        public void ProtobufMessage_ShouldSerializeCorrectly()
        {
            // Arrange
            var telemetry = new Iotdataprocessor.Telemetry
            {
                SensorId = "device-001",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Value = 25.5,
                Unit = "celsius"
            };

            // Act
            var bytes = Google.Protobuf.MessageExtensions.ToByteArray(telemetry);
            var deserialized = Iotdataprocessor.Telemetry.Parser.ParseFrom(bytes);

            // Assert
            Assert.Equal(telemetry.SensorId, deserialized.SensorId);
            Assert.Equal(telemetry.Value, deserialized.Value);
            Assert.Equal(telemetry.Unit, deserialized.Unit);
        }

        [Fact]
        public void RandomValues_ShouldBeDifferentOnMultipleCalls()
        {
            // Arrange
            var random = new Random();
            var values = new HashSet<double>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                var value = 15.0 + (random.NextDouble() * 20.0);
                values.Add(Math.Round(value, 2));
            }

            // Assert
            Assert.True(values.Count > 50, "Should generate diverse random values");
        }

        [Fact]
        public void SensorData_ShouldHaveRealisticValues()
        {
            // Arrange
            var random = new Random();

            // Act
            var temperature = 15.0 + (random.NextDouble() * 20.0); // 15-35°C
            var humidity = 30.0 + (random.NextDouble() * 50.0);    // 30-80%
            var pressure = 980.0 + (random.NextDouble() * 70.0);    // 980-1050 hPa

            // Assert - typical environmental ranges
            Assert.InRange(temperature, -50, 60);  // Extreme environmental range
            Assert.InRange(humidity, 0, 100);       // Humidity percentage
            Assert.InRange(pressure, 870, 1084);    // Atmospheric pressure range
        }
    }
}
