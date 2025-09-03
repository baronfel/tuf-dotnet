using TUnit.Assertions;
using TUnit.Core;
using TUF.Http;

namespace TUF.Tests;

public class HttpResilienceBasicTests
{
    [Test]
    public async Task HttpResilienceConfig_DefaultValues_AreReasonable()
    {
        // Arrange & Act
        var config = new HttpResilienceConfig();

        // Assert
        await Assert.That(config.MaxRetries).IsEqualTo(3);
        await Assert.That(config.BaseDelay).IsEqualTo(TimeSpan.FromSeconds(1));
        await Assert.That(config.MaxDelay).IsEqualTo(TimeSpan.FromSeconds(30));
        await Assert.That(config.RequestTimeout).IsEqualTo(TimeSpan.FromSeconds(30));
        await Assert.That(config.UserAgent).IsEqualTo("TUF-DotNet/1.0");
        await Assert.That(config.RetryStatusCodes).IsNotNull();
    }

    [Test]
    public async Task ResilientHttpClient_Constructor_AcceptsValidParameters()
    {
        // Arrange
        var httpClient = new HttpClient();
        var config = new HttpResilienceConfig();

        // Act
        var resilientClient = new ResilientHttpClient(httpClient, config);

        // Assert
        await Assert.That(resilientClient).IsNotNull();
    }
}