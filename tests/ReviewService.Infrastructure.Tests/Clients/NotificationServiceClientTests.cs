using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using ReviewService.Infrastructure.Clients;
using ReviewService.Infrastructure.Tests.Helpers;

namespace ReviewService.Infrastructure.Tests.Clients;

[TestFixture]
public class NotificationServiceClientTests
{
    private Mock<HttpMessageHandler> _mockHandler = null!;
    private Mock<ILogger<NotificationServiceClient>> _mockLogger = null!;
    private HttpClient _httpClient = null!;
    private NotificationServiceClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        _mockLogger = new Mock<ILogger<NotificationServiceClient>>();

        _httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("https://fake-notification-service.com")
        };

        _client = new NotificationServiceClient(_httpClient, _mockLogger.Object);
    }

    [TearDown]
    public void Cleanup()
    {
        _httpClient.Dispose();
    }

    // ============================================
    // SendReviewApprovedAsync Tests
    // ============================================

    [Test]
    public async Task SendReviewApprovedAsync_ShouldSucceed_WhenServiceReturnsOk()
    {
        // ARRANGE
        var email = "test@example.com";
        var reviewId = Guid.NewGuid();

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-approved")
            .ReturnsResponse(HttpStatusCode.OK);

        // ACT
        await _client.SendReviewApprovedAsync(email, reviewId);

        // ASSERT - Should not throw
        Assert.Pass("Notification sent successfully");
    }

    [Test]
    public async Task SendReviewApprovedAsync_ShouldNotThrow_WhenServiceReturnsError()
    {
        // ARRANGE
        var email = "test@example.com";
        var reviewId = Guid.NewGuid();

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-approved")
            .ReturnsResponse(HttpStatusCode.InternalServerError);

        // ACT & ASSERT - Should not throw, just log
        Assert.DoesNotThrowAsync(async () => 
            await _client.SendReviewApprovedAsync(email, reviewId)
        );
    }

    [Test]
    public async Task SendReviewApprovedAsync_ShouldNotThrow_OnNetworkError()
    {
        // ARRANGE
        var email = "test@example.com";
        var reviewId = Guid.NewGuid();

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-approved")
            .Throws<HttpRequestException>();

        // ACT & ASSERT - Should not throw, just log
        Assert.DoesNotThrowAsync(async () => 
            await _client.SendReviewApprovedAsync(email, reviewId)
        );
    }

    [Test]
    public async Task SendReviewApprovedAsync_ShouldNotThrow_OnTimeout()
    {
        // ARRANGE
        var email = "test@example.com";
        var reviewId = Guid.NewGuid();

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-approved")
            .Throws<TaskCanceledException>();

        // ACT & ASSERT - Should not throw, just log
        Assert.DoesNotThrowAsync(async () => 
            await _client.SendReviewApprovedAsync(email, reviewId)
        );
    }

    // ============================================
    // SendReviewRejectedAsync Tests
    // ============================================

    [Test]
    public async Task SendReviewRejectedAsync_ShouldSucceed_WhenServiceReturnsOk()
    {
        // ARRANGE
        var email = "test@example.com";
        var reviewId = Guid.NewGuid();
        var reasons = new List<string> { "Spam detected", "Prohibited content" };

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-rejected")
            .ReturnsResponse(HttpStatusCode.OK);

        // ACT
        await _client.SendReviewRejectedAsync(email, reviewId, reasons);

        // ASSERT - Should not throw
        Assert.Pass("Rejection notification sent successfully");
    }

    [Test]
    public async Task SendReviewRejectedAsync_ShouldNotThrow_WhenServiceReturnsError()
    {
        // ARRANGE
        var email = "test@example.com";
        var reviewId = Guid.NewGuid();
        var reasons = new List<string> { "Spam detected" };

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-rejected")
            .ReturnsResponse(HttpStatusCode.BadRequest);

        // ACT & ASSERT - Should not throw, just log
        Assert.DoesNotThrowAsync(async () => 
            await _client.SendReviewRejectedAsync(email, reviewId, reasons)
        );
    }

    [Test]
    public async Task SendReviewRejectedAsync_ShouldNotThrow_OnNetworkError()
    {
        // ARRANGE
        var email = "test@example.com";
        var reviewId = Guid.NewGuid();
        var reasons = new List<string> { "Invalid content" };

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-rejected")
            .Throws<HttpRequestException>();

        // ACT & ASSERT - Should not throw, just log
        Assert.DoesNotThrowAsync(async () => 
            await _client.SendReviewRejectedAsync(email, reviewId, reasons)
        );
    }

    [Test]
    public async Task SendReviewRejectedAsync_ShouldHandleEmptyReasons()
    {
        // ARRANGE
        var email = "test@example.com";
        var reviewId = Guid.NewGuid();
        var reasons = new List<string>(); // Empty list

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-rejected")
            .ReturnsResponse(HttpStatusCode.OK);

        // ACT & ASSERT - Should not throw
        Assert.DoesNotThrowAsync(async () => 
            await _client.SendReviewRejectedAsync(email, reviewId, reasons)
        );
    }

    // ============================================
    // SendReviewFlaggedAsync Tests
    // ============================================

    [Test]
    public async Task SendReviewFlaggedAsync_ShouldSucceed_WhenServiceReturnsOk()
    {
        // ARRANGE
        var email = "test@example.com";
        var reviewId = Guid.NewGuid();

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-flagged")
            .ReturnsResponse(HttpStatusCode.OK);

        // ACT
        await _client.SendReviewFlaggedAsync(email, reviewId);

        // ASSERT - Should not throw
        Assert.Pass("Flagged notification sent successfully");
    }

    [Test]
    public async Task SendReviewFlaggedAsync_ShouldNotThrow_WhenServiceReturnsError()
    {
        // ARRANGE
        var email = "test@example.com";
        var reviewId = Guid.NewGuid();

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-flagged")
            .ReturnsResponse(HttpStatusCode.ServiceUnavailable);

        // ACT & ASSERT - Should not throw, just log
        Assert.DoesNotThrowAsync(async () => 
            await _client.SendReviewFlaggedAsync(email, reviewId)
        );
    }

    [Test]
    public async Task SendReviewFlaggedAsync_ShouldNotThrow_OnNetworkError()
    {
        // ARRANGE
        var email = "test@example.com";
        var reviewId = Guid.NewGuid();

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-flagged")
            .Throws<HttpRequestException>();

        // ACT & ASSERT - Should not throw, just log
        Assert.DoesNotThrowAsync(async () => 
            await _client.SendReviewFlaggedAsync(email, reviewId)
        );
    }

    [Test]
    public async Task SendReviewFlaggedAsync_ShouldNotThrow_OnTimeout()
    {
        // ARRANGE
        var email = "test@example.com";
        var reviewId = Guid.NewGuid();

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-flagged")
            .Throws<TaskCanceledException>();

        // ACT & ASSERT - Should not throw, just log
        Assert.DoesNotThrowAsync(async () => 
            await _client.SendReviewFlaggedAsync(email, reviewId)
        );
    }

    // ============================================
    // Integration-like Tests (Multiple notifications)
    // ============================================

    [Test]
    public async Task MultipleNotifications_ShouldAllSucceed_WhenServiceIsHealthy()
    {
        // ARRANGE
        var email = "test@example.com";
        var reviewId1 = Guid.NewGuid();
        var reviewId2 = Guid.NewGuid();
        var reviewId3 = Guid.NewGuid();

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-approved")
            .ReturnsResponse(HttpStatusCode.OK);

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-rejected")
            .ReturnsResponse(HttpStatusCode.OK);

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/notification/review-flagged")
            .ReturnsResponse(HttpStatusCode.OK);

        // ACT & ASSERT - All should succeed
        Assert.DoesNotThrowAsync(async () =>
        {
            await _client.SendReviewApprovedAsync(email, reviewId1);
            await _client.SendReviewRejectedAsync(email, reviewId2, new List<string> { "Test reason" });
            await _client.SendReviewFlaggedAsync(email, reviewId3);
        });
    }
}