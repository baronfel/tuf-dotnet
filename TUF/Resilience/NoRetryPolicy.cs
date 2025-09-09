namespace TUF.Resilience;

/// <summary>
/// No-retry policy that executes operations exactly once without retry logic.
/// Useful for scenarios where immediate failure is preferred or during testing.
/// </summary>
public sealed class NoRetryPolicy : IRetryPolicy
{
    /// <summary>
    /// Singleton instance of the no-retry policy
    /// </summary>
    public static readonly NoRetryPolicy Instance = new();

    private NoRetryPolicy() { }

    /// <inheritdoc />
    public int MaxAttempts => 1;

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return await operation(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await operation(cancellationToken);
    }

    /// <inheritdoc />
    public bool ShouldRetry(Exception exception, int attemptNumber) => false;

    /// <inheritdoc />
    public TimeSpan GetRetryDelay(int attemptNumber) => TimeSpan.Zero;
}