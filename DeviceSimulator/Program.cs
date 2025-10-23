using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using Google.Protobuf;
using System.Security.Cryptography;

namespace DeviceSimulator
{
    class Program
    {
        private static readonly Random _random = new Random();
        private static IMqttClient? _mqttClient;
        private static string _deviceId = "simulated-device-001";
        private static string _iotHubHost = "iot-iot-data-processor-dev.azure-devices.net";
        private static string _deviceKey = "your-device-key-here"; // Replace with actual device key

        // Sensor configurations
        private static readonly string[] _sensorTypes = { "temperature", "pressure", "humidity", "vibration" };
        private static readonly (double min, double max, string unit)[] _sensorRanges = {
            (-10, 50, "celsius"),      // temperature
            (900, 1200, "hPa"),        // pressure
            (0, 100, "percent"),       // humidity
            (0, 10, "mm/s")            // vibration
        };

        static async Task Main(string[] args)
        {
            Console.WriteLine("IoT Device Simulator starting...");
            Console.WriteLine($"Device ID: {_deviceId}");
            Console.WriteLine($"IoT Hub: {_iotHubHost}");

            // Parse command line arguments
            if (args.Length >= 1) _deviceId = args[0];
            if (args.Length >= 2) _iotHubHost = args[1];
            if (args.Length >= 3) _deviceKey = args[2];

            try
            {
                await InitializeMqttClient();
                await ConnectToIoTHub();

                Console.WriteLine("Connected to IoT Hub. Starting telemetry transmission...");
                Console.WriteLine("Press Ctrl+C to stop.");

                // Set up cancellation token for graceful shutdown
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                await RunTelemetryLoop(cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return;
            }
        }

        private static async Task InitializeMqttClient()
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            // Set up event handlers
            _mqttClient.ConnectedAsync += OnConnected;
            _mqttClient.DisconnectedAsync += OnDisconnected;
            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;
        }

        private static async Task ConnectToIoTHub()
        {
            if (_mqttClient == null) throw new InvalidOperationException("MQTT client not initialized");

            // Create MQTT client options for Azure IoT Hub
            var options = new MqttClientOptionsBuilder()
                .WithClientId(_deviceId)
                .WithTcpServer(_iotHubHost, 8883)
                .WithCredentials($"{_iotHubHost}/{_deviceId}/?api-version=2021-04-12", GenerateSasToken())
                .WithTlsOptions(new MqttClientTlsOptionsBuilder().Build())
                .WithCleanSession()
                .Build();

            var result = await _mqttClient.ConnectAsync(options);
            if (result.ResultCode != MqttClientConnectResultCode.Success)
            {
                throw new Exception($"Failed to connect to IoT Hub: {result.ResultCode}");
            }
        }

        private static async Task RunTelemetryLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await SendTelemetryMessage();
                    await Task.Delay(5000, cancellationToken); // Send every 5 seconds
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending telemetry: {ex.Message}");
                    await Task.Delay(1000, cancellationToken); // Wait before retry
                }
            }
        }

        private static async Task SendTelemetryMessage()
        {
            if (_mqttClient == null) throw new InvalidOperationException("MQTT client not connected");

            // Generate random telemetry data
            var sensorIndex = _random.Next(_sensorTypes.Length);
            var sensorType = _sensorTypes[sensorIndex];
            var (min, max, unit) = _sensorRanges[sensorIndex];

            var telemetry = new Iotdataprocessor.Telemetry
            {
                SensorId = $"{_deviceId}-{sensorType}",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Value = min + (_random.NextDouble() * (max - min)),
                Unit = unit
            };

            // Add some metadata
            telemetry.Metadata.Add("device_type", "simulator");
            telemetry.Metadata.Add("location", "lab");
            telemetry.Metadata.Add("firmware_version", "1.0.0");

            // Serialize to Protobuf
            var messageBytes = telemetry.ToByteArray();

            // Create MQTT message
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("devices/" + _deviceId + "/messages/events/")
                .WithPayload(messageBytes)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(false)
                .Build();

            // Send message
            await _mqttClient.PublishAsync(message);

            Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Sent telemetry: Sensor={telemetry.SensorId}, Value={telemetry.Value:F2} {telemetry.Unit}");
        }

        private static string GenerateSasToken()
        {
            // Generate SAS token for Azure IoT Hub
            var expiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
            var audience = $"{_iotHubHost}/devices/{_deviceId}";
            var signature = GenerateSignature(audience, expiry.ToString());

            return $"SharedAccessSignature sr={Uri.EscapeDataString(audience)}&sig={Uri.EscapeDataString(signature)}&se={expiry}";
        }

        private static string GenerateSignature(string audience, string expiry)
        {
            using var hmac = new HMACSHA256(Convert.FromBase64String(_deviceKey));
            var message = $"{audience}\n{expiry}";
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return Convert.ToBase64String(hash);
        }

        private static Task OnConnected(MqttClientConnectedEventArgs arg)
        {
            Console.WriteLine("Connected to IoT Hub");
            return Task.CompletedTask;
        }

        private static Task OnDisconnected(MqttClientDisconnectedEventArgs arg)
        {
            Console.WriteLine($"Disconnected from IoT Hub: {arg.Reason}");
            return Task.CompletedTask;
        }

        private static Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs arg)
        {
            Console.WriteLine($"Received message: {arg.ApplicationMessage.Topic}");
            return Task.CompletedTask;
        }
    }
}
