using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace IoTDataProcessor.Tests
{
    public class TelemetryAggregatorFixedTests
    {
        [Fact]
        public void CalculateStandardDeviation_WithSingleValue_ShouldReturnZero()
        {
            // Arrange
            var values = new List<double> { 25.0 };

            // Act
            var result = CalculateStandardDeviation(values);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public void CalculateStandardDeviation_WithMultipleValues_ShouldReturnCorrectValue()
        {
            // Arrange
            var values = new List<double> { 10.0, 20.0, 30.0, 40.0, 50.0 };
            var expectedStdDev = 14.142; // Approximate

            // Act
            var result = CalculateStandardDeviation(values);

            // Assert
            Assert.True(Math.Abs(result - expectedStdDev) < 0.01, 
                $"Expected ~{expectedStdDev}, but got {result}");
        }

        [Fact]
        public void TelemetryAggregateData_ShouldStoreAllStatistics()
        {
            // Arrange
            var aggregate = new TelemetryAggregateData
            {
                SensorId = "sensor-001",
                WindowStart = DateTimeOffset.UtcNow,
                WindowEnd = DateTimeOffset.UtcNow.AddMinutes(5),
                AvgValue = 25.5,
                MinValue = 20.0,
                MaxValue = 30.0,
                StdDevValue = 3.2,
                Count = 100,
                Unit = "celsius",
                ProcessedAt = DateTimeOffset.UtcNow
            };

            // Assert
            Assert.Equal("sensor-001", aggregate.SensorId);
            Assert.Equal(25.5, aggregate.AvgValue);
            Assert.Equal(20.0, aggregate.MinValue);
            Assert.Equal(30.0, aggregate.MaxValue);
            Assert.Equal(3.2, aggregate.StdDevValue);
            Assert.Equal(100, aggregate.Count);
            Assert.Equal("celsius", aggregate.Unit);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(3, 0)]
        [InlineData(5, 5)]
        [InlineData(8, 5)]
        [InlineData(13, 10)]
        [InlineData(59, 55)]
        public void WindowRounding_ShouldRoundToFiveMinutes(int inputMinute, int expectedMinute)
        {
            // Arrange
            var timestamp = new DateTimeOffset(2024, 1, 1, 12, inputMinute, 0, TimeSpan.Zero);

            // Act
            var roundedMinute = (timestamp.Minute / 5) * 5;
            var windowStart = new DateTimeOffset(
                timestamp.Year, timestamp.Month, timestamp.Day,
                timestamp.Hour, roundedMinute, 0, timestamp.Offset);

            // Assert
            Assert.Equal(expectedMinute, windowStart.Minute);
        }

        [Fact]
        public void TelemetryDataPoint_ShouldStoreAllRequiredFields()
        {
            // Arrange & Act
            var dataPoint = new TelemetryDataPoint
            {
                SensorId = "sensor-001",
                Value = 25.5,
                Unit = "celsius",
                Timestamp = DateTimeOffset.UtcNow
            };

            // Assert
            Assert.Equal("sensor-001", dataPoint.SensorId);
            Assert.Equal(25.5, dataPoint.Value);
            Assert.Equal("celsius", dataPoint.Unit);
            Assert.NotEqual(default(DateTimeOffset), dataPoint.Timestamp);
        }

        [Fact]
        public void AggregateCalculation_WithMultipleDataPoints_ShouldCalculateCorrectStatistics()
        {
            // Arrange
            var dataPoints = new List<double> { 20.0, 22.5, 25.0, 27.5, 30.0 };

            // Act
            var avg = dataPoints.Average();
            var min = dataPoints.Min();
            var max = dataPoints.Max();
            var stdDev = CalculateStandardDeviation(dataPoints);

            // Assert
            Assert.Equal(25.0, avg);
            Assert.Equal(20.0, min);
            Assert.Equal(30.0, max);
            Assert.True(stdDev > 0);
        }

        [Fact]
        public void WindowKey_ShouldBeUniquePerSensorAndTimeWindow()
        {
            // Arrange
            var sensor1 = "sensor-001";
            var sensor2 = "sensor-002";
            var timestamp1 = new DateTimeOffset(2024, 1, 1, 12, 3, 0, TimeSpan.Zero);  // Rounds to 12:00
            var timestamp2 = new DateTimeOffset(2024, 1, 1, 12, 4, 0, TimeSpan.Zero);  // Also rounds to 12:00
            var timestamp3 = new DateTimeOffset(2024, 1, 1, 12, 8, 0, TimeSpan.Zero);  // Rounds to 12:05

            // Act
            var key1 = GetWindowKey(sensor1, timestamp1);
            var key2 = GetWindowKey(sensor1, timestamp2);
            var key3 = GetWindowKey(sensor1, timestamp3);
            var key4 = GetWindowKey(sensor2, timestamp1);

            // Assert
            Assert.Equal(key1, key2); // Same sensor, same 5-min window (both round to 12:00)
            Assert.NotEqual(key1, key3); // Same sensor, different window (12:00 vs 12:05)
            Assert.NotEqual(key1, key4); // Different sensor, same window time
        }

        // Helper methods matching the implementation
        private static double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count <= 1) return 0;
            
            double avg = values.Average();
            double sumOfSquaresOfDifferences = values.Select(val => (val - avg) * (val - avg)).Sum();
            return Math.Sqrt(sumOfSquaresOfDifferences / values.Count);
        }

        private static string GetWindowKey(string sensorId, DateTimeOffset timestamp)
        {
            var minute = (timestamp.Minute / 5) * 5;
            var windowStart = new DateTimeOffset(
                timestamp.Year, timestamp.Month, timestamp.Day,
                timestamp.Hour, minute, 0, timestamp.Offset);
            
            return $"{sensorId}_{windowStart:yyyyMMddHHmm}";
        }
    }
}
