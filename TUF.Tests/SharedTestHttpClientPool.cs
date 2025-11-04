using System.Collections.Concurrent;

namespace TUF.Tests;

/// <summary>
/// Shared HttpClient pool for tests to reduce overhead of creating HttpClient instances.
/// This improves test performance by reusing HttpClient instances across test methods.
/// </summary>
public static class SharedTestHttpClientPool
{
    private static readonly ConcurrentBag<HttpClient> _availableClients = new();
    private static readonly ConcurrentBag<HttpClient> _allClients = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Gets an HttpClient instance from the pool or creates a new one if none are available.
    /// </summary>
    /// <returns>An HttpClient instance ready for use.</returns>
    public static HttpClient GetClient()
    {
        if (_availableClients.TryTake(out var client))
        {
            return client;
        }

        lock (_lock)
        {
            // Double-check in case another thread created one
            if (_availableClients.TryTake(out client))
            {
                return client;
            }

            // Create new client
            client = new HttpClient();
            _allClients.Add(client);
            return client;
        }
    }

    /// <summary>
    /// Returns an HttpClient instance to the pool for reuse.
    /// </summary>
    /// <param name="client">The HttpClient to return to the pool.</param>
    public static void ReturnClient(HttpClient client)
    {
        if (client != null)
        {
            _availableClients.Add(client);
        }
    }

    /// <summary>
    /// Gets an HttpClient with a custom message handler from the pool or creates a new one.
    /// </summary>
    /// <param name="handler">The HttpMessageHandler to use.</param>
    /// <returns>An HttpClient instance with the specified handler.</returns>
    public static HttpClient GetClientWithHandler(HttpMessageHandler handler)
    {
        // For custom handlers, we create a new client each time
        // as pooling with different handlers would be complex
        var client = new HttpClient(handler);
        _allClients.Add(client);
        return client;
    }

    /// <summary>
    /// Disposes all HttpClient instances in the pool. Called during test cleanup.
    /// </summary>
    internal static void DisposeAll()
    {
        // Dispose available clients
        while (_availableClients.TryTake(out var client))
        {
            client.Dispose();
        }

        // Dispose all clients that were created
        foreach (var client in _allClients)
        {
            try
            {
                client.Dispose();
            }
            catch
            {
                // Ignore disposal errors during cleanup
            }
        }
    }
}