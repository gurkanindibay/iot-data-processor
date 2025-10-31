using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace IoTDataProcessor.Logging
{
    /// <summary>
    /// Configuration options for blob logging
    /// </summary>
    public class BlobLoggerOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string ContainerName { get; set; } = "application-logs";
        public int MaxFileSizeMB { get; set; } = 100;
        public int RetentionDays { get; set; } = 30;
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    }

    /// <summary>
    /// Logger that writes to Azure Blob Storage
    /// </summary>
    public class BlobLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly BlobLoggerOptions _options;
        private readonly BlobContainerClient _containerClient;
        private readonly ConcurrentQueue<LogEntry> _logQueue = new();
        private readonly Timer _flushTimer;
        private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
        private bool _disposed;

        public BlobLogger(string categoryName, BlobLoggerOptions options)
        {
            _categoryName = categoryName;
            _options = options;

            _containerClient = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
            _containerClient.CreateIfNotExistsAsync(PublicAccessType.None).GetAwaiter().GetResult();

            // Flush logs every 30 seconds
            _flushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _options.MinimumLogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                LogLevel = logLevel,
                Category = _categoryName,
                EventId = eventId.Id,
                Message = message,
                Exception = exception?.ToString()
            };

            _logQueue.Enqueue(logEntry);
        }

        private async void FlushLogs(object? state)
        {
            if (_disposed || _logQueue.IsEmpty)
                return;

            await _flushSemaphore.WaitAsync();
            try
            {
                var logs = new System.Collections.Generic.List<LogEntry>();
                while (_logQueue.TryDequeue(out var logEntry))
                {
                    logs.Add(logEntry);
                }

                if (logs.Count > 0)
                {
                    await WriteLogsToBlobAsync(logs);
                }
            }
            catch (Exception ex)
            {
                // In a production scenario, you might want to log this to a fallback logger
                Console.WriteLine($"Error flushing logs to blob storage: {ex.Message}");
            }
            finally
            {
                _flushSemaphore.Release();
            }
        }

        private async Task WriteLogsToBlobAsync(System.Collections.Generic.List<LogEntry> logs)
        {
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var blobName = $"{date}/logs-{DateTime.UtcNow:HH-mm-ss}-{Guid.NewGuid()}.json";

            var blobClient = _containerClient.GetBlobClient(blobName);

            var logContent = new StringBuilder();
            logContent.AppendLine("[");

            for (int i = 0; i < logs.Count; i++)
            {
                var log = logs[i];
                logContent.AppendLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    timestamp = log.Timestamp.ToString("O"),
                    level = log.LogLevel.ToString(),
                    category = log.Category,
                    eventId = log.EventId,
                    message = log.Message,
                    exception = log.Exception
                }));

                if (i < logs.Count - 1)
                    logContent.AppendLine(",");
            }

            logContent.AppendLine("]");

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(logContent.ToString()));
            await blobClient.UploadAsync(stream, new BlobHttpHeaders
            {
                ContentType = "application/json"
            }, conditions: null);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _flushTimer?.Dispose();

            // Final flush
            FlushLogs(null);
        }
    }

    /// <summary>
    /// Logger provider for blob storage
    /// </summary>
    public class BlobLoggerProvider : ILoggerProvider
    {
        private readonly BlobLoggerOptions _options;
        private readonly ConcurrentDictionary<string, BlobLogger> _loggers = new();

        public BlobLoggerProvider(IOptions<BlobLoggerOptions> options)
        {
            _options = options.Value;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new BlobLogger(name, _options));
        }

        public void Dispose()
        {
            foreach (var logger in _loggers.Values)
            {
                logger.Dispose();
            }
            _loggers.Clear();
        }
    }

    /// <summary>
    /// Represents a log entry
    /// </summary>
    internal class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel LogLevel { get; set; }
        public string Category { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
    }

    /// <summary>
    /// Extension methods for adding blob logging
    /// </summary>
    public static class BlobLoggingExtensions
    {
        public static ILoggingBuilder AddBlobLogging(this ILoggingBuilder builder, Action<BlobLoggerOptions> configure)
        {
            builder.Services.Configure(configure);
            builder.Services.AddSingleton<ILoggerProvider, BlobLoggerProvider>();
            return builder;
        }
    }
}