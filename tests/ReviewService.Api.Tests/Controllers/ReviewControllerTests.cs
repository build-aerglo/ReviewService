using Dapr.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ReviewService.Api.Controllers;
using ReviewService.Application.DTOs;
using ReviewService.Application.Services;
using ReviewService.Domain.Exceptions;

namespace ReviewService.Api.Tests.Controllers;

[TestFixture]
public class ReviewControllerTests
{
    private Mock<IReviewService> _mockReviewService = null!;
    private Mock<DaprClient> _mockDaprClient = null!;
    private Mock<ILogger<ReviewController>> _mockLogger = null!;
    private ReviewController _controller = null!;

    [SetUp]
    public void Setup()
    {
        _mockReviewService = new Mock<IReviewService>();
        _mockDaprClient = new Mock<DaprClient>();
        _mockLogger = new Mock<ILogger<ReviewController>>();
        _controller = new ReviewController(_mockReviewService.Object, _mockDaprClient.Object, _mockLogger.Object);

        // Setup HttpContext for metadata extraction
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Test]
    public async Task CreateReview_ShouldReturnAccepted_WhenSuccessful()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        var dto = new CreateReviewDto(
            BusinessId: businessId,
            LocationId: null,
            ReviewerId: Guid.NewGuid(),
            Email: null,
            StarRating: 5,
            ReviewBody: "Excellent service! Very satisfied with everything.",
            PhotoUrls: null,
            ReviewAsAnon: false
        );

        var response = new ReviewResponseDto(
            Id: Guid.NewGuid(),
            BusinessId: businessId,
            LocationId: null,
            ReviewerId: dto.ReviewerId,
            Email: null,
            StarRating: 5,
            ReviewBody: "Excellent service! Very satisfied with everything.",
            PhotoUrls: null,
            ReviewAsAnon: false,
            CreatedAt: DateTime.UtcNow,
            Status: "PENDING",
            ValidatedAt: null
        );

        _mockReviewService
            .Setup(s => s.CreateReviewAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(response);

        _mockDaprClient
            .Setup(d => d.PublishEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReviewSubmittedMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // ACT
        var result = await _controller.CreateReview(dto);

        // ASSERT
        var acceptedResult = result as AcceptedResult;
        Assert.That(acceptedResult, Is.Not.Null);
        Assert.That(acceptedResult!.StatusCode, Is.EqualTo(202));

        var returnedValue = acceptedResult.Value;
        Assert.That(returnedValue, Is.Not.Null);

        _mockReviewService.Verify(s => s.CreateReviewAsync(
            dto, 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>()), Times.Once);

        _mockDaprClient.Verify(d => d.PublishEventAsync(
            "review-pubsub",
            "review-submitted",
            It.IsAny<ReviewSubmittedMessage>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateReview_ShouldReturnNotFound_WhenBusinessDoesNotExist()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        var dto = new CreateReviewDto(
            BusinessId: businessId,
            LocationId: null,
            ReviewerId: null,
            Email: "test@example.com",
            StarRating: 4,
            ReviewBody: "Business doesn't exist but trying to review.",
            PhotoUrls: null,
            ReviewAsAnon: true
        );

        _mockReviewService
            .Setup(s => s.CreateReviewAsync(dto, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new BusinessNotFoundException(businessId));

        // ACT
        var result = await _controller.CreateReview(dto);

        // ASSERT
        var notFoundResult = result as NotFoundObjectResult;
        Assert.That(notFoundResult, Is.Not.Null);
        Assert.That(notFoundResult!.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task GetReview_ShouldReturnOk_WhenReviewExists()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var response = new ReviewResponseDto(
            Id: reviewId,
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: null,
            Email: "test@example.com",
            StarRating: 4,
            ReviewBody: "Good service overall. Happy with result.",
            PhotoUrls: null,
            ReviewAsAnon: true,
            CreatedAt: DateTime.UtcNow,
            Status: "APPROVED",
            ValidatedAt: DateTime.UtcNow
        );

        _mockReviewService
            .Setup(s => s.GetReviewByIdAsync(reviewId))
            .ReturnsAsync(response);

        // ACT
        var result = await _controller.GetReview(reviewId);

        // ASSERT
        var okResult = result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);
        Assert.That(okResult!.StatusCode, Is.EqualTo(200));

        var returnedValue = okResult.Value as ReviewResponseDto;
        Assert.That(returnedValue!.Id, Is.EqualTo(reviewId));
        Assert.That(returnedValue.Status, Is.EqualTo("APPROVED"));
    }

    [Test]
    public async Task GetBusinessReviews_ShouldReturnOnlyApprovedReviews()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        var reviews = new List<ReviewResponseDto>
        {
            new ReviewResponseDto(
                Id: Guid.NewGuid(),
                BusinessId: businessId,
                LocationId: null,
                ReviewerId: null,
                Email: "user1@example.com",
                StarRating: 5,
                ReviewBody: "Excellent! Highly recommend this business.",
                PhotoUrls: null,
                ReviewAsAnon: false,
                CreatedAt: DateTime.UtcNow,
                Status: "APPROVED",
                ValidatedAt: DateTime.UtcNow
            )
        };

        _mockReviewService
            .Setup(s => s.GetReviewsByBusinessIdAsync(businessId))
            .ReturnsAsync(reviews);

        // ACT
        var result = await _controller.GetBusinessReviews(businessId);

        // ASSERT
        var okResult = result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);

        var returnedReviews = okResult!.Value as IEnumerable<ReviewResponseDto>;
        Assert.That(returnedReviews, Is.Not.Null);
        Assert.That(returnedReviews!.All(r => r.Status == "APPROVED"), Is.True);
    }

    [Test]
    public async Task GetReviewStatus_ShouldReturnStatus_WhenEmailMatches()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var email = "test@example.com";
        var statusDto = new ReviewStatusDto(
            ReviewId: reviewId,
            Status: "APPROVED",
            ValidatedAt: DateTime.UtcNow,
            ValidationResult: null
        );

        _mockReviewService
            .Setup(s => s.GetReviewStatusAsync(reviewId, email))
            .ReturnsAsync(statusDto);

        // ACT
        var result = await _controller.GetReviewStatus(reviewId, email);

        // ASSERT
        var okResult = result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);

        var returnedStatus = okResult!.Value as ReviewStatusDto;
        Assert.That(returnedStatus!.Status, Is.EqualTo("APPROVED"));
    }

    [Test]
    public async Task GetReviewStatus_ShouldReturnBadRequest_WhenEmailMissing()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();

        // ACT
        var result = await _controller.GetReviewStatus(reviewId, null);

        // ASSERT
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult, Is.Not.Null);
    }

    [Test]
    public async Task UpdateReview_ShouldReturnOk_WhenSuccessful()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var dto = new UpdateReviewDto(
            StarRating: 5,
            ReviewBody: "Updated: Excellent service! Changed my mind.",
            PhotoUrls: new[] { "photo1.jpg" },
            ReviewAsAnon: true
        );

        var response = new ReviewResponseDto(
            Id: reviewId,
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: reviewerId,
            Email: null,
            StarRating: 5,
            ReviewBody: "Updated: Excellent service! Changed my mind.",
            PhotoUrls: new[] { "photo1.jpg" },
            ReviewAsAnon: true,
            CreatedAt: DateTime.UtcNow.AddDays(-1),
            Status: "APPROVED",
            ValidatedAt: DateTime.UtcNow
        );

        _mockReviewService
            .Setup(s => s.UpdateReviewAsync(reviewId, dto, reviewerId, null))
            .ReturnsAsync(response);

        // ACT
        var result = await _controller.UpdateReview(reviewId, dto, reviewerId, null);

        // ASSERT
        var okResult = result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);
        Assert.That(okResult!.StatusCode, Is.EqualTo(200));
    }

    [Test]
    public async Task DeleteReview_ShouldReturnNoContent_WhenSuccessful()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        _mockReviewService
            .Setup(s => s.DeleteReviewAsync(reviewId, reviewerId, null))
            .Returns(Task.CompletedTask);

        // ACT
        var result = await _controller.DeleteReview(reviewId, reviewerId, null);

        // ASSERT
        var noContentResult = result as NoContentResult;
        Assert.That(noContentResult, Is.Not.Null);
        Assert.That(noContentResult!.StatusCode, Is.EqualTo(204));
    }
}