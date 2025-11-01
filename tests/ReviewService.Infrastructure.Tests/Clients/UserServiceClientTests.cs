using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using ReviewService.Infrastructure.Clients;
using ReviewService.Infrastructure.Tests.Helpers;

namespace ReviewService.Infrastructure.Tests.Clients;

[TestFixture]
public class UserServiceClientTests
{
    private Mock<HttpMessageHandler> _mockHandler = null!;
    private Mock<ILogger<UserServiceClient>> _mockLogger = null!;
    private HttpClient _httpClient = null!;
    private UserServiceClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        _mockLogger = new Mock<ILogger<UserServiceClient>>();

        _httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("https://fake-user-service.com")
        };

        _client = new UserServiceClient(_httpClient, _mockLogger.Object);
    }

    [TearDown]
    public void Cleanup()
    {
        _httpClient.Dispose();
    }

    // ✅ ESSENTIAL TEST: Happy path - user exists
    [Test]
    public async Task UserExistsAsync_ShouldReturnTrue_WhenStatusOk()
    {
        // ARRANGE
        var userId = Guid.NewGuid();
        _mockHandler
            .SetupRequest(HttpMethod.Get, $"/api/users/{userId}")
            .ReturnsResponse(HttpStatusCode.OK);

        // ACT
        var result = await _client.UserExistsAsync(userId);

        // ASSERT
        Assert.That(result, Is.True);
    }

    // ✅ ESSENTIAL TEST: User not found
    [Test]
    public async Task UserExistsAsync_ShouldReturnFalse_WhenStatusNotFound()
    {
        // ARRANGE
        var userId = Guid.NewGuid();
        _mockHandler
            .SetupRequest(HttpMethod.Get, $"/api/users/{userId}")
            .ReturnsResponse(HttpStatusCode.NotFound);

        // ACT
        var result = await _client.UserExistsAsync(userId);

        // ASSERT
        Assert.That(result, Is.False);
    }

    // ✅ EDGE CASE: Unexpected status code
    [Test]
    public async Task UserExistsAsync_ShouldReturnFalse_OnUnexpectedStatus()
    {
        // ARRANGE
        var userId = Guid.NewGuid();
        _mockHandler
            .SetupRequest(HttpMethod.Get, $"/api/users/{userId}")
            .ReturnsResponse(HttpStatusCode.InternalServerError);

        // ACT
        var result = await _client.UserExistsAsync(userId);

        // ASSERT
        Assert.That(result, Is.False);
    }

    // ✅ ESSENTIAL TEST: Network error handling
    [Test]
    public async Task UserExistsAsync_ShouldReturnFalse_OnHttpRequestException()
    {
        // ARRANGE
        var userId = Guid.NewGuid();
        _mockHandler
            .SetupRequest(HttpMethod.Get, $"/api/users/{userId}")
            .Throws<HttpRequestException>();

        // ACT
        var result = await _client.UserExistsAsync(userId);

        // ASSERT
        Assert.That(result, Is.False);
    }
}