using Microsoft.Extensions.Logging;
using Moq;
using ReviewService.Application.DTOs;
using ReviewService.Application.Interfaces;
using ReviewService.Application.Services;
using ReviewService.Domain.Entities;
using ReviewService.Domain.Repositories;

namespace ReviewService.Application.Tests.Services;

[TestFixture]
public class ReviewValidationServiceTests
{
    private Mock<IReviewRepository> _mockReviewRepository = null!;
    private Mock<IComplianceServiceClient> _mockComplianceClient = null!;
    private Mock<INotificationServiceClient> _mockNotificationClient = null!;
    private Mock<ILogger<ReviewValidationService>> _mockLogger = null!;
    private ReviewValidationService _service = null!;

    [SetUp]
    public void Setup()
    {
        _mockReviewRepository = new Mock<IReviewRepository>();
        _mockComplianceClient = new Mock<IComplianceServiceClient>();
        _mockNotificationClient = new Mock<INotificationServiceClient>();
        _mockLogger = new Mock<ILogger<ReviewValidationService>>();

        _service = new ReviewValidationService(
            _mockReviewRepository.Object,
            _mockComplianceClient.Object,
            _mockNotificationClient.Object,
            _mockLogger.Object
        );
    }

    // ✅ ESSENTIAL: Valid review should be approved
    [Test]
    public async Task ProcessReviewAsync_ValidReview_ShouldUpdateToApproved()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var email = "test@example.com";

        var message = new ReviewSubmittedMessage(
            ReviewId: reviewId,
            BusinessId: businessId,
            LocationId: null,
            ReviewerId: null,
            Email: email,
            StarRating: 5,
            ReviewBody: "This is a great business with excellent service!",
            PhotoUrls: null,
            ReviewAsAnon: false,
            IpAddress: "192.168.1.1",
            DeviceId: "device-123",
            Geolocation: "Lagos, Lagos, Nigeria",
            UserAgent: "Mozilla/5.0",
            CreatedAt: DateTime.UtcNow
        );

        var validationResult = new ValidationResult(
            IsValid: true,
            Level: 3,
            Errors: new List<string>(),
            Warnings: new List<string>(),
            ExecutedRules: new List<string> { "CharacterLimit", "Frequency", "SpamDetection" },
            Timestamp: DateTime.UtcNow
        );

        var review = new Review(
            businessId, null, null, email, 5,
            "This is a great business with excellent service!",
            null, false,
            "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0"
        );

        _mockComplianceClient
            .Setup(c => c.ValidateReviewAsync(It.IsAny<ValidateReviewRequest>()))
            .ReturnsAsync(validationResult);

        _mockReviewRepository
            .Setup(r => r.GetByIdAsync(reviewId))
            .ReturnsAsync(review);

        _mockReviewRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Review>()))
            .Returns(Task.CompletedTask);

        _mockNotificationClient
            .Setup(n => n.SendReviewApprovedAsync(email, reviewId))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ProcessReviewAsync(message);

        // Assert
        _mockComplianceClient.Verify(c => c.ValidateReviewAsync(
            It.Is<ValidateReviewRequest>(req =>
                req.ReviewId == reviewId &&
                req.BusinessId == businessId &&
                req.Email == email
            )), Times.Once);

        _mockReviewRepository.Verify(r => r.UpdateAsync(
            It.Is<Review>(rev => rev.Status == ReviewStatus.Approved)
        ), Times.Once);

        _mockNotificationClient.Verify(n => n.SendReviewApprovedAsync(email, reviewId), Times.Once);
    }

    // ✅ ESSENTIAL: Invalid review should be rejected
    [Test]
public async Task ProcessReviewAsync_InvalidReview_ShouldUpdateToRejected()
{
    // Arrange
    var reviewId = Guid.NewGuid();
    var businessId = Guid.NewGuid();
    var email = "test@example.com";

    var message = new ReviewSubmittedMessage(
        ReviewId: reviewId,
        BusinessId: businessId,
        LocationId: null,
        ReviewerId: null,
        Email: email,
        StarRating: 5,
        ReviewBody: "This is a test review that will be rejected by compliance.", // ✅ FIXED: Now 20+ chars
        PhotoUrls: null,
        ReviewAsAnon: false,
        IpAddress: "192.168.1.1",
        DeviceId: "device-123",
        Geolocation: "Lagos, Lagos, Nigeria",
        UserAgent: "Mozilla/5.0",
        CreatedAt: DateTime.UtcNow
    );

    var validationResult = new ValidationResult(
        IsValid: false,
        Level: 1,
        Errors: new List<string> { "Review contains prohibited content" }, 
        Warnings: new List<string>(),
        ExecutedRules: new List<string> { "ContentPolicy" },
        Timestamp: DateTime.UtcNow
    );

    var review = new Review(
        businessId, null, null, email, 5, 
        "This is a test review that will be rejected by compliance.", 
        null, false,
        "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0"
    );

    _mockComplianceClient
        .Setup(c => c.ValidateReviewAsync(It.IsAny<ValidateReviewRequest>()))
        .ReturnsAsync(validationResult);

    _mockReviewRepository
        .Setup(r => r.GetByIdAsync(reviewId))
        .ReturnsAsync(review);

    _mockReviewRepository
        .Setup(r => r.UpdateAsync(It.IsAny<Review>()))
        .Returns(Task.CompletedTask);

    _mockNotificationClient
        .Setup(n => n.SendReviewRejectedAsync(email, reviewId, It.IsAny<List<string>>()))
        .Returns(Task.CompletedTask);

    // Act
    await _service.ProcessReviewAsync(message);

    // Assert
    _mockReviewRepository.Verify(r => r.UpdateAsync(
        It.Is<Review>(rev => rev.Status == ReviewStatus.Rejected)
    ), Times.Once);

    _mockNotificationClient.Verify(n => n.SendReviewRejectedAsync(
        email, reviewId, It.Is<List<string>>(errors => errors.Count > 0)
    ), Times.Once);
}

    // ✅ ESSENTIAL: Review with warnings should be flagged
    [Test]
    public async Task ProcessReviewAsync_ReviewWithWarnings_ShouldUpdateToFlagged()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var email = "test@example.com";

        var message = new ReviewSubmittedMessage(
            ReviewId: reviewId,
            BusinessId: businessId,
            LocationId: null,
            ReviewerId: null,
            Email: email,
            StarRating: 5,
            ReviewBody: "This is a great business with excellent service!",
            PhotoUrls: null,
            ReviewAsAnon: false,
            IpAddress: "192.168.1.1",
            DeviceId: "device-123",
            Geolocation: "Port Harcourt, Rivers, Nigeria",
            UserAgent: "Mozilla/5.0",
            CreatedAt: DateTime.UtcNow
        );

        var validationResult = new ValidationResult(
            IsValid: true,
            Level: 3,
            Errors: new List<string>(),
            Warnings: new List<string> { "Reviewer location (Rivers) differs from business location (Lagos)" },
            ExecutedRules: new List<string> { "CharacterLimit", "Geolocation" },
            Timestamp: DateTime.UtcNow
        );

        var review = new Review(
            businessId, null, null, email, 5,
            "This is a great business with excellent service!",
            null, false,
            "192.168.1.1", "device-123", "Port Harcourt, Rivers, Nigeria", "Mozilla/5.0"
        );

        _mockComplianceClient
            .Setup(c => c.ValidateReviewAsync(It.IsAny<ValidateReviewRequest>()))
            .ReturnsAsync(validationResult);

        _mockReviewRepository
            .Setup(r => r.GetByIdAsync(reviewId))
            .ReturnsAsync(review);

        _mockReviewRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Review>()))
            .Returns(Task.CompletedTask);

        _mockNotificationClient
            .Setup(n => n.SendReviewFlaggedAsync(email, reviewId))
            .Returns(Task.CompletedTask);

        // Act
        await _service.ProcessReviewAsync(message);

        // Assert
        _mockReviewRepository.Verify(r => r.UpdateAsync(
            It.Is<Review>(rev => rev.Status == ReviewStatus.Flagged)
        ), Times.Once);

        _mockNotificationClient.Verify(n => n.SendReviewFlaggedAsync(email, reviewId), Times.Once);
    }

    // ✅ ESSENTIAL: Handle review not found
    [Test]
    public async Task ProcessReviewAsync_ReviewNotFound_ShouldLogErrorAndNotCrash()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var message = new ReviewSubmittedMessage(
            ReviewId: reviewId,
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: null,
            Email: "test@example.com",
            StarRating: 5,
            ReviewBody: "Test review",
            PhotoUrls: null,
            ReviewAsAnon: false,
            IpAddress: "192.168.1.1",
            DeviceId: "device-123",
            Geolocation: "Lagos, Lagos, Nigeria",
            UserAgent: "Mozilla/5.0",
            CreatedAt: DateTime.UtcNow
        );

        _mockComplianceClient
            .Setup(c => c.ValidateReviewAsync(It.IsAny<ValidateReviewRequest>()))
            .ReturnsAsync(new ValidationResult(true, 3, new List<string>(), new List<string>(), new List<string>(), DateTime.UtcNow));

        _mockReviewRepository
            .Setup(r => r.GetByIdAsync(reviewId))
            .ReturnsAsync((Review?)null);

        // Act
        await _service.ProcessReviewAsync(message);

        // Assert
        _mockReviewRepository.Verify(r => r.UpdateAsync(It.IsAny<Review>()), Times.Never);
        _mockNotificationClient.Verify(n => n.SendReviewApprovedAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }
}