using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using ReviewService.Infrastructure.Clients;
using ReviewService.Infrastructure.Tests.Helpers;

namespace ReviewService.Infrastructure.Tests.Clients;

[TestFixture]
public class BusinessServiceClientTests
{
    private Mock<HttpMessageHandler> _mockHandler = null!;
    private Mock<ILogger<BusinessServiceClient>> _mockLogger = null!;
    private HttpClient _httpClient = null!;
    private BusinessServiceClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        _mockLogger = new Mock<ILogger<BusinessServiceClient>>();

        _httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("https://fake-business-service.com")
        };

        _client = new BusinessServiceClient(_httpClient, _mockLogger.Object);
    }

    [TearDown]
    public void Cleanup()
    {
        _httpClient.Dispose();
    }

    // ✅ ESSENTIAL TEST: Happy path - business exists
    [Test]
    public async Task BusinessExistsAsync_ShouldReturnTrue_WhenStatusOk()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        _mockHandler
            .SetupRequest(HttpMethod.Get, $"/api/Business/{businessId}")
            .ReturnsResponse(HttpStatusCode.OK);

        // ACT
        var result = await _client.BusinessExistsAsync(businessId);

        // ASSERT
        Assert.That(result, Is.True);
    }

    // ✅ ESSENTIAL TEST: Business not found
    [Test]
    public async Task BusinessExistsAsync_ShouldReturnFalse_WhenStatusNotFound()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        _mockHandler
            .SetupRequest(HttpMethod.Get, $"/api/Business/{businessId}")
            .ReturnsResponse(HttpStatusCode.NotFound);

        // ACT
        var result = await _client.BusinessExistsAsync(businessId);

        // ASSERT
        Assert.That(result, Is.False);
    }

    // ✅ EDGE CASE: Unexpected status code
    [Test]
    public async Task BusinessExistsAsync_ShouldReturnFalse_OnUnexpectedStatus()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        _mockHandler
            .SetupRequest(HttpMethod.Get, $"/api/Business/{businessId}")
            .ReturnsResponse(HttpStatusCode.InternalServerError);

        // ACT
        var result = await _client.BusinessExistsAsync(businessId);

        // ASSERT
        Assert.That(result, Is.False);
    }

    // ✅ ESSENTIAL TEST: Network error handling
    [Test]
    public async Task BusinessExistsAsync_ShouldReturnFalse_OnHttpRequestException()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        _mockHandler
            .SetupRequest(HttpMethod.Get, $"/api/Business/{businessId}")
            .Throws<HttpRequestException>();

        // ACT
        var result = await _client.BusinessExistsAsync(businessId);

        // ASSERT
        Assert.That(result, Is.False);
    }
}