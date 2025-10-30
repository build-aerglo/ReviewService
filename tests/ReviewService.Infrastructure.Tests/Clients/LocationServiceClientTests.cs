using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using ReviewService.Infrastructure.Clients;
using ReviewService.Infrastructure.Tests.Helpers;

namespace ReviewService.Infrastructure.Tests.Clients;

[TestFixture]
public class LocationServiceClientTests
{
    private Mock<HttpMessageHandler> _mockHandler = null!;
    private Mock<ILogger<LocationServiceClient>> _mockLogger = null!;
    private HttpClient _httpClient = null!;
    private LocationServiceClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        _mockLogger = new Mock<ILogger<LocationServiceClient>>();

        _httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("https://fake-location-service.com")
        };

        _client = new LocationServiceClient(_httpClient, _mockLogger.Object);
    }

    [TearDown]
    public void Cleanup()
    {
        _httpClient.Dispose();
    }

    // ✅ ESSENTIAL TEST: Happy path - location exists
    [Test]
    public async Task LocationExistsAsync_ShouldReturnTrue_WhenStatusOk()
    {
        // ARRANGE
        var locationId = Guid.NewGuid();
        _mockHandler
            .SetupRequest(HttpMethod.Get, $"/api/locations/{locationId}")
            .ReturnsResponse(HttpStatusCode.OK);

        // ACT
        var result = await _client.LocationExistsAsync(locationId);

        // ASSERT
        Assert.That(result, Is.True);
    }

    // ✅ ESSENTIAL TEST: Location not found
    [Test]
    public async Task LocationExistsAsync_ShouldReturnFalse_WhenStatusNotFound()
    {
        // ARRANGE
        var locationId = Guid.NewGuid();
        _mockHandler
            .SetupRequest(HttpMethod.Get, $"/api/locations/{locationId}")
            .ReturnsResponse(HttpStatusCode.NotFound);

        // ACT
        var result = await _client.LocationExistsAsync(locationId);

        // ASSERT
        Assert.That(result, Is.False);
    }

    // ✅ EDGE CASE: Unexpected status code
    [Test]
    public async Task LocationExistsAsync_ShouldReturnFalse_OnUnexpectedStatus()
    {
        // ARRANGE
        var locationId = Guid.NewGuid();
        _mockHandler
            .SetupRequest(HttpMethod.Get, $"/api/locations/{locationId}")
            .ReturnsResponse(HttpStatusCode.InternalServerError);

        // ACT
        var result = await _client.LocationExistsAsync(locationId);

        // ASSERT
        Assert.That(result, Is.False);
    }

    // ✅ ESSENTIAL TEST: Network error handling
    [Test]
    public async Task LocationExistsAsync_ShouldReturnFalse_OnHttpRequestException()
    {
        // ARRANGE
        var locationId = Guid.NewGuid();
        _mockHandler
            .SetupRequest(HttpMethod.Get, $"/api/locations/{locationId}")
            .Throws<HttpRequestException>();

        // ACT
        var result = await _client.LocationExistsAsync(locationId);

        // ASSERT
        Assert.That(result, Is.False);
    }
}