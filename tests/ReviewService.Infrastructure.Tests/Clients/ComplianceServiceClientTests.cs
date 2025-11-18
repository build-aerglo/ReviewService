using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Moq;
using ReviewService.Application.DTOs;
using ReviewService.Infrastructure.Clients;
using ReviewService.Infrastructure.Tests.Helpers;

namespace ReviewService.Infrastructure.Tests.Clients;

[TestFixture]
public class ComplianceServiceClientTests
{
    private Mock<HttpMessageHandler> _mockHandler = null!;
    private Mock<ILogger<ComplianceServiceClient>> _mockLogger = null!;
    private HttpClient _httpClient = null!;
    private ComplianceServiceClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        _mockLogger = new Mock<ILogger<ComplianceServiceClient>>();

        _httpClient = new HttpClient(_mockHandler.Object)
        {
            BaseAddress = new Uri("https://fake-compliance-service.com")
        };

        _client = new ComplianceServiceClient(_httpClient, _mockLogger.Object);
    }

    [TearDown]
    public void Cleanup()
    {
        _httpClient.Dispose();
    }

    // ✅ ESSENTIAL TEST: Valid review passes all checks
    [Test]
    public async Task ValidateReviewAsync_ShouldReturnValid_WhenReviewPassesAllChecks()
    {
        // ARRANGE
        var request = new ValidateReviewRequest(
            ReviewId: Guid.NewGuid(),
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: null,
            Email: "test@example.com",
            StarRating: 5,
            ReviewBody: "Excellent service! Very satisfied with everything.",
            IpAddress: "192.168.1.1",
            DeviceId: "device-123",
            Geolocation: "Lagos, Lagos, Nigeria",
            UserAgent: "Mozilla/5.0",
            IsGuestUser: true
        );

        var expectedResponse = new ValidationResult(
            IsValid: true,
            Level: 3,
            Errors: new List<string>(),
            Warnings: new List<string>(),
            ExecutedRules: new List<string> { "CharacterLimit", "Frequency", "SpamDetection" },
            Timestamp: DateTime.UtcNow
        );

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/compliance/validate-review")
            .ReturnsJsonResponse(expectedResponse);

        // ACT
        var result = await _client.ValidateReviewAsync(request);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Level, Is.EqualTo(3));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.ExecutedRules, Has.Count.EqualTo(3));
        });
    }

    // ✅ ESSENTIAL TEST: Invalid review with errors
    [Test]
    public async Task ValidateReviewAsync_ShouldReturnInvalid_WhenReviewHasErrors()
    {
        // ARRANGE
        var request = new ValidateReviewRequest(
            ReviewId: Guid.NewGuid(),
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: null,
            Email: "spam@example.com",
            StarRating: 5,
            ReviewBody: "This review contains spam content and should be rejected.",
            IpAddress: "192.168.1.1",
            DeviceId: "device-123",
            Geolocation: "Lagos, Lagos, Nigeria",
            UserAgent: "Mozilla/5.0",
            IsGuestUser: true
        );

        var expectedResponse = new ValidationResult(
            IsValid: false,
            Level: 1,
            Errors: new List<string> { "Spam content detected", "Prohibited keywords found" },
            Warnings: new List<string>(),
            ExecutedRules: new List<string> { "SpamDetection", "ContentPolicy" },
            Timestamp: DateTime.UtcNow
        );

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/compliance/validate-review")
            .ReturnsJsonResponse(expectedResponse);

        // ACT
        var result = await _client.ValidateReviewAsync(request);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Level, Is.EqualTo(1));
            Assert.That(result.Errors, Has.Count.EqualTo(2));
            Assert.That(result.Errors[0], Does.Contain("Spam"));
        });
    }

    // ✅ ESSENTIAL TEST: Valid review with warnings (should be flagged)
    [Test]
    public async Task ValidateReviewAsync_ShouldReturnValidWithWarnings_WhenReviewNeedsManualReview()
    {
        // ARRANGE
        var request = new ValidateReviewRequest(
            ReviewId: Guid.NewGuid(),
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: null,
            Email: "test@example.com",
            StarRating: 5,
            ReviewBody: "Great service! Very satisfied with everything here.",
            IpAddress: "192.168.1.1",
            DeviceId: "device-123",
            Geolocation: "Port Harcourt, Rivers, Nigeria",
            UserAgent: "Mozilla/5.0",
            IsGuestUser: true
        );

        var expectedResponse = new ValidationResult(
            IsValid: true,
            Level: 3,
            Errors: new List<string>(),
            Warnings: new List<string> { "Reviewer location differs from business location", "High frequency detected" },
            ExecutedRules: new List<string> { "Geolocation", "Frequency" },
            Timestamp: DateTime.UtcNow
        );

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/compliance/validate-review")
            .ReturnsJsonResponse(expectedResponse);

        // ACT
        var result = await _client.ValidateReviewAsync(request);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Warnings, Has.Count.EqualTo(2));
            Assert.That(result.Warnings, Does.Contain("Reviewer location differs from business location"));
        });
    }

    // ✅ EDGE CASE: Compliance service returns error status
    [Test]
    public async Task ValidateReviewAsync_ShouldReturnFailedValidation_WhenServiceReturnsError()
    {
        // ARRANGE
        var request = new ValidateReviewRequest(
            ReviewId: Guid.NewGuid(),
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: null,
            Email: "test@example.com",
            StarRating: 5,
            ReviewBody: "Test review with sufficient length for validation.",
            IpAddress: "192.168.1.1",
            DeviceId: "device-123",
            Geolocation: "Lagos, Lagos, Nigeria",
            UserAgent: "Mozilla/5.0",
            IsGuestUser: true
        );

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/compliance/validate-review")
            .ReturnsResponse(HttpStatusCode.InternalServerError);

        // ACT
        var result = await _client.ValidateReviewAsync(request);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Level, Is.EqualTo(0));
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("Validation service unavailable"));
        });
    }

    // ✅ EDGE CASE: Network error
    [Test]
    public async Task ValidateReviewAsync_ShouldReturnFailedValidation_OnNetworkError()
    {
        // ARRANGE
        var request = new ValidateReviewRequest(
            ReviewId: Guid.NewGuid(),
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: null,
            Email: "test@example.com",
            StarRating: 5,
            ReviewBody: "Test review with sufficient length for validation.",
            IpAddress: "192.168.1.1",
            DeviceId: "device-123",
            Geolocation: "Lagos, Lagos, Nigeria",
            UserAgent: "Mozilla/5.0",
            IsGuestUser: true
        );

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/compliance/validate-review")
            .Throws<HttpRequestException>();

        // ACT
        var result = await _client.ValidateReviewAsync(request);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("Network error"));
        });
    }

    // ✅ EDGE CASE: Invalid JSON response
    [Test]
    public async Task ValidateReviewAsync_ShouldReturnFailedValidation_WhenResponseIsInvalid()
    {
        // ARRANGE
        var request = new ValidateReviewRequest(
            ReviewId: Guid.NewGuid(),
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: null,
            Email: "test@example.com",
            StarRating: 5,
            ReviewBody: "Test review with sufficient length for validation.",
            IpAddress: "192.168.1.1",
            DeviceId: "device-123",
            Geolocation: "Lagos, Lagos, Nigeria",
            UserAgent: "Mozilla/5.0",
            IsGuestUser: true
        );

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/compliance/validate-review")
            .ReturnsResponse(HttpStatusCode.OK, "invalid json response");

        // ACT
        var result = await _client.ValidateReviewAsync(request);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            // ✅ FIXED: Changed from "Invalid validation response" to "Unexpected validation error"
            Assert.That(result.Errors[0], Does.Contain("Unexpected validation error"));
        });
    }

    // ✅ TEST: Duplicate detection scenario
    [Test]
    public async Task ValidateReviewAsync_ShouldReturnInvalid_WhenDuplicateDetected()
    {
        // ARRANGE
        var request = new ValidateReviewRequest(
            ReviewId: Guid.NewGuid(),
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: null,
            Email: "duplicate@example.com",
            StarRating: 5,
            ReviewBody: "This is a duplicate review submission within 72 hours.",
            IpAddress: "192.168.1.1",
            DeviceId: "device-123",
            Geolocation: "Lagos, Lagos, Nigeria",
            UserAgent: "Mozilla/5.0",
            IsGuestUser: true
        );

        var expectedResponse = new ValidationResult(
            IsValid: false,
            Level: 1,
            Errors: new List<string> { "Duplicate review detected within 72 hours" },
            Warnings: new List<string>(),
            ExecutedRules: new List<string> { "DuplicateCheck" },
            Timestamp: DateTime.UtcNow
        );

        _mockHandler
            .SetupRequest(HttpMethod.Post, "/api/compliance/validate-review")
            .ReturnsJsonResponse(expectedResponse);

        // ACT
        var result = await _client.ValidateReviewAsync(request);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors[0], Does.Contain("Duplicate"));
        });
    }
}
