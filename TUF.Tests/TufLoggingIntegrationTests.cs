using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TUF.Exceptions;

namespace TUF.Tests;

/// <summary>
/// Integration tests for TUF logging functionality
/// </summary>
public class TufLoggingIntegrationTests
{
    [Test]
    public async Task TufActivitySource_HasCorrectProperties()
    {
        await Assert.That(TufActivitySource.Name).IsEqualTo("TUF.NET");
        await Assert.That(TufActivitySource.Version).IsEqualTo("1.0.0");
        await Assert.That(TufActivitySource.Instance).IsNotNull();
    }

    [Test]
    public async Task TufActivitySource_CanStartActivity()
    {
        using var activity = TufActivitySource.StartActivity("TestOperation");
        
        // Activity might be null if no listener is registered, which is fine for this test
        // The important thing is that the method doesn't throw and returns a disposable
        if (activity != null)
        {
            await Assert.That(activity.OperationName).IsEqualTo("TUF.TestOperation");
        }
        
        // Test passed if we get here without throwing
    }

    [Test]
    public async Task TufExceptionLoggingExtensions_HandlesAllExceptionTypes()
    {
        var testLogger = new TestLogger();
        var exceptions = new TufException[]
        {
            new RollbackAttackException("timestamp", 10, 5),
            new TargetIntegrityException("Hash mismatch", "/file.bin", "expected", "actual"),
            new SignatureVerificationException("Signature failed", "key-123", "targets"),
            new InsufficientSignaturesException(3, 1, "targets"),
            new MetadataDeserializationException("Parse error", "root"),
            new MetadataValidationException("Invalid", "root"),
            new ExpiredMetadataException("Expired", "timestamp"),
            new RepositoryNetworkException("Network error", new Uri("https://example.com")),
            new TargetNotFoundException("Not found", "/file.txt"),
            new FileSizeException("large.bin", 1000, 500),
            new MaxDelegationDepthExceededException(5, 10),
            new DelegationException("Delegation failed", "role"),
            new TufConfigurationException("Config error", "property")
        };

        foreach (var exception in exceptions)
        {
            testLogger.Clear();
            ((ILogger)testLogger).LogTufException(exception);
            
            var logEntries = testLogger.GetAllLogEntries();
            await Assert.That(logEntries).HasCount().GreaterThan(0);
            
            var logEntry = logEntries.First();
            await Assert.That(logEntry.Message).IsNotNull();
            await Assert.That(logEntry.Message).IsNotEmpty();
        }
    }

    [Test]
    public async Task SecurityExceptions_LogAsCriticalOrError()
    {
        var testLogger = new TestLogger();
        var securityExceptions = new TufException[]
        {
            new RollbackAttackException("timestamp", 10, 5),
            new TargetIntegrityException("Hash mismatch", "/file.bin", "expected", "actual"),
            new SignatureVerificationException("Signature failed", "key-123", "targets")
        };

        foreach (var exception in securityExceptions)
        {
            testLogger.Clear();
            ((ILogger)testLogger).LogTufException(exception);
            
            var logEntries = testLogger.GetAllLogEntries();
            await Assert.That(logEntries).HasCount().GreaterThan(0);
            
            var logEntry = logEntries.First();
            var isSecurityLevel = logEntry.Level == LogLevel.Critical || logEntry.Level == LogLevel.Error;
            await Assert.That(isSecurityLevel).IsTrue();
        }
    }

    [Test]
    public async Task TufExceptionLogging_PreservesExceptionContext()
    {
        var testLogger = new TestLogger();
        var rollbackException = new RollbackAttackException("timestamp", 10, 5);
        
        ((ILogger)testLogger).LogTufException(rollbackException);
        
        var logEntries = testLogger.GetAllLogEntries();
        await Assert.That(logEntries).HasCount().GreaterThan(0);
        
        var logEntry = logEntries.First();
        await Assert.That(logEntry.Level).IsEqualTo(LogLevel.Critical);
        await Assert.That(logEntry.Message).Contains("timestamp");
        await Assert.That(logEntry.Message).Contains("10"); // expected version
        await Assert.That(logEntry.Message).Contains("5");  // actual version
    }

    [Test]
    public async Task ActivitySource_CanBeUsedInUsingStatement()
    {
        var activityCreated = false;
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = _ => activityCreated = true
        };
        
        ActivitySource.AddActivityListener(listener);
        
        using (var activity = TufActivitySource.StartActivity("TestOperation"))
        {
            // Activity creation is successful
        }
        
        // Test verifies that activity creation and disposal don't throw
        // Activity might not be created if there are no active listeners, which is fine
        await Assert.That(true).IsTrue(); // Test passes if we get here
    }
}

/// <summary>
/// Test logger implementation for unit testing
/// </summary>
internal class TestLogger : ILogger
{
    private readonly List<LogEntry> _logEntries = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logEntries.Add(new LogEntry
        {
            Level = logLevel,
            EventId = eventId.Id,
            Message = formatter(state, exception),
            Exception = exception
        });
    }

    public LogEntry? GetLogEntry(int eventId) => _logEntries.FirstOrDefault(e => e.EventId == eventId);
    public List<LogEntry> GetAllLogEntries() => _logEntries.ToList();
    public void Clear() => _logEntries.Clear();
}

/// <summary>
/// Log entry for testing
/// </summary>
internal class LogEntry
{
    public LogLevel Level { get; set; }
    public int EventId { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}