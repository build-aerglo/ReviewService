using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
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
    private Mock<ILogger<ReviewController>> _mockLogger = null!;
    private ReviewController _controller = null!;

    [SetUp]
    public void Setup()
    {
        _mockReviewService = new Mock<IReviewService>();
        _mockLogger = new Mock<ILogger<ReviewController>>();
        _controller = new ReviewController(_mockReviewService.Object, _mockLogger.Object);
    }

    [Test]
    public async Task CreateReview_ShouldReturnCreated_WhenSuccessful()
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
            CreatedAt: DateTime.UtcNow
        );

        _mockReviewService
            .Setup(s => s.CreateReviewAsync(dto))
            .ReturnsAsync(response);

        var mockUrlHelper = new Mock<IUrlHelper>();
        mockUrlHelper
            .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
            .Returns("/api/review/" + response.Id);

        _controller.Url = mockUrlHelper.Object;

        // ACT
        var result = await _controller.CreateReview(dto);

        // ASSERT
        var createdResult = result as CreatedResult;
        Assert.That(createdResult, Is.Not.Null);
        Assert.That(createdResult!.StatusCode, Is.EqualTo(201));

        var returnedValue = createdResult.Value as ReviewResponseDto;
        Assert.That(returnedValue, Is.Not.Null);
        Assert.That(returnedValue!.StarRating, Is.EqualTo(5));
        Assert.That(returnedValue.BusinessId, Is.EqualTo(businessId));

        _mockReviewService.Verify(s => s.CreateReviewAsync(dto), Times.Once);
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
            .Setup(s => s.CreateReviewAsync(dto))
            .ThrowsAsync(new BusinessNotFoundException(businessId));

        // ACT
        var result = await _controller.CreateReview(dto);

        // ASSERT
        var notFoundResult = result as NotFoundObjectResult;
        Assert.That(notFoundResult, Is.Not.Null);
        Assert.That(notFoundResult!.StatusCode, Is.EqualTo(404));
        Assert.That(notFoundResult.Value?.ToString(), Does.Contain(businessId.ToString()));
    }

    [Test]
    public async Task CreateReview_ShouldReturnBadRequest_WhenInvalidData()
    {
        // ARRANGE
        var dto = new CreateReviewDto(
            BusinessId: Guid.NewGuid(),
            LocationId: Guid.NewGuid(),
            ReviewerId: null,
            Email: "test@example.com",
            StarRating: 5,
            ReviewBody: "Testing invalid location scenario.",
            PhotoUrls: null,
            ReviewAsAnon: false
        );

        _mockReviewService
            .Setup(s => s.CreateReviewAsync(dto))
            .ThrowsAsync(new InvalidReviewDataException("Location does not exist."));

        // ACT
        var result = await _controller.CreateReview(dto);

        // ASSERT
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult, Is.Not.Null);
        Assert.That(badRequestResult!.StatusCode, Is.EqualTo(400));
        Assert.That(badRequestResult.Value?.ToString(), Does.Contain("Location does not exist."));
    }

    [Test]
    public async Task CreateReview_ShouldReturnBadRequest_WhenValidationFails()
    {
        // ARRANGE
        var dto = new CreateReviewDto(
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: null,
            Email: "test@example.com",
            StarRating: 6, // Invalid: > 5
            ReviewBody: "Short", // Invalid: < 20 chars
            PhotoUrls: null,
            ReviewAsAnon: false
        );

        _mockReviewService
            .Setup(s => s.CreateReviewAsync(dto))
            .ThrowsAsync(new ArgumentException("Star rating must be between 1 and 5."));

        // ACT
        var result = await _controller.CreateReview(dto);

        // ASSERT
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult, Is.Not.Null);
        Assert.That(badRequestResult!.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task CreateReview_ShouldReturnInternalServerError_WhenCreationFails()
    {
        // ARRANGE
        var dto = new CreateReviewDto(
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: null,
            Email: "fail@example.com",
            StarRating: 4,
            ReviewBody: "This should fail at database level.",
            PhotoUrls: null,
            ReviewAsAnon: false
        );

        _mockReviewService
            .Setup(s => s.CreateReviewAsync(dto))
            .ThrowsAsync(new ReviewCreationFailedException("Failed to create review record."));

        // ACT
        var result = await _controller.CreateReview(dto);

        // ASSERT
        var errorResult = result as ObjectResult;
        Assert.That(errorResult, Is.Not.Null);
        Assert.That(errorResult!.StatusCode, Is.EqualTo(500));
        Assert.That(errorResult.Value?.ToString(), Does.Contain("Failed to create review record."));
    }

    [Test]
    public async Task CreateReview_ShouldReturnInternalServerError_WhenUnexpectedErrorOccurs()
    {
        // ARRANGE
        var dto = new CreateReviewDto(
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: null,
            Email: "unexpected@example.com",
            StarRating: 3,
            ReviewBody: "Unexpected error should occur during processing.",
            PhotoUrls: null,
            ReviewAsAnon: false
        );

        _mockReviewService
            .Setup(s => s.CreateReviewAsync(dto))
            .ThrowsAsync(new Exception("Unexpected failure"));

        // ACT
        var result = await _controller.CreateReview(dto);

        // ASSERT
        var errorResult = result as ObjectResult;
        Assert.That(errorResult, Is.Not.Null);
        Assert.That(errorResult!.StatusCode, Is.EqualTo(500));
        Assert.That(errorResult.Value?.ToString(), Does.Contain("Internal server error occurred."));
    }

    [Test]
    public async Task CreateReview_ShouldReturnBadRequest_WhenModelStateInvalid()
    {
        // ARRANGE
        var dto = new CreateReviewDto(
            BusinessId: Guid.Empty,
            LocationId: null,
            ReviewerId: null,
            Email: "",
            StarRating: 0,
            ReviewBody: "",
            PhotoUrls: null,
            ReviewAsAnon: false
        );

        _controller.ModelState.AddModelError("BusinessId", "BusinessId is required");

        // ACT
        var result = await _controller.CreateReview(dto);

        // ASSERT
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult, Is.Not.Null);
        Assert.That(badRequestResult!.StatusCode, Is.EqualTo(400));
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
            CreatedAt: DateTime.UtcNow
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
    }

    [Test]
    public async Task GetReview_ShouldReturnNotFound_WhenReviewDoesNotExist()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        _mockReviewService
            .Setup(s => s.GetReviewByIdAsync(reviewId))
            .ReturnsAsync((ReviewResponseDto?)null);

        // ACT
        var result = await _controller.GetReview(reviewId);

        // ASSERT
        var notFoundResult = result as NotFoundResult;
        Assert.That(notFoundResult, Is.Not.Null);
        Assert.That(notFoundResult!.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task GetBusinessReviews_ShouldReturnOk_WhenReviewsExist()
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
                CreatedAt: DateTime.UtcNow
            ),
            new ReviewResponseDto(
                Id: Guid.NewGuid(),
                BusinessId: businessId,
                LocationId: null,
                ReviewerId: Guid.NewGuid(),
                Email: null,
                StarRating: 4,
                ReviewBody: "Very good service. Will come back again.",
                PhotoUrls: null,
                ReviewAsAnon: true,
                CreatedAt: DateTime.UtcNow
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
        Assert.That(okResult!.StatusCode, Is.EqualTo(200));

        var returnedReviews = okResult.Value as IEnumerable<ReviewResponseDto>;
        Assert.That(returnedReviews!.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetBusinessReviews_ShouldReturnEmptyList_WhenNoReviews()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        _mockReviewService
            .Setup(s => s.GetReviewsByBusinessIdAsync(businessId))
            .ReturnsAsync(new List<ReviewResponseDto>());

        // ACT
        var result = await _controller.GetBusinessReviews(businessId);

        // ASSERT
        var okResult = result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);

        var returnedReviews = okResult!.Value as IEnumerable<ReviewResponseDto>;
        Assert.That(returnedReviews, Is.Empty);
    }

    // ======================================
    // UPDATE REVIEW CONTROLLER TESTS
    // ======================================

    [Test]
    public async Task UpdateReview_ShouldReturnOk_WhenSuccessful_RegisteredUser()
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
            CreatedAt: DateTime.UtcNow.AddDays(-1)
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

        var returnedValue = okResult.Value as ReviewResponseDto;
        Assert.That(returnedValue!.StarRating, Is.EqualTo(5));
        Assert.That(returnedValue.ReviewBody, Does.Contain("Updated:"));
        Assert.That(returnedValue.ReviewAsAnon, Is.True);

        _mockReviewService.Verify(s => s.UpdateReviewAsync(reviewId, dto, reviewerId, null), Times.Once);
    }

    [Test]
    public async Task UpdateReview_ShouldReturnOk_WhenSuccessful_GuestUser()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var email = "guest@example.com";
        var dto = new UpdateReviewDto(
            StarRating: 4,
            ReviewBody: "Updated: Actually pretty good service.",
            PhotoUrls: null,
            ReviewAsAnon: false
        );

        var response = new ReviewResponseDto(
            Id: reviewId,
            BusinessId: Guid.NewGuid(),
            LocationId: null,
            ReviewerId: null,
            Email: email,
            StarRating: 4,
            ReviewBody: "Updated: Actually pretty good service.",
            PhotoUrls: null,
            ReviewAsAnon: false,
            CreatedAt: DateTime.UtcNow.AddDays(-2)
        );

        _mockReviewService
            .Setup(s => s.UpdateReviewAsync(reviewId, dto, null, email))
            .ReturnsAsync(response);

        // ACT
        var result = await _controller.UpdateReview(reviewId, dto, null, email);

        // ASSERT
        var okResult = result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);
        Assert.That(okResult!.StatusCode, Is.EqualTo(200));

        var returnedValue = okResult.Value as ReviewResponseDto;
        Assert.That(returnedValue!.Email, Is.EqualTo(email));
        Assert.That(returnedValue.StarRating, Is.EqualTo(4));
    }

    [Test]
    public async Task UpdateReview_ShouldReturnBadRequest_WhenNoAuthorizationProvided()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var dto = new UpdateReviewDto(5, "Updated review", null, null);

        // ACT
        var result = await _controller.UpdateReview(reviewId, dto, null, null);

        // ASSERT
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult, Is.Not.Null);
        Assert.That(badRequestResult!.StatusCode, Is.EqualTo(400));
        Assert.That(badRequestResult.Value?.ToString(), Does.Contain("Either reviewerId or email must be provided"));

        _mockReviewService.Verify(s => s.UpdateReviewAsync(
            It.IsAny<Guid>(), It.IsAny<UpdateReviewDto>(), It.IsAny<Guid?>(), It.IsAny<string?>()), Times.Never);
    }

    [Test]
    public async Task UpdateReview_ShouldReturnNotFound_WhenReviewDoesNotExist()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var dto = new UpdateReviewDto(5, "Updated review for non-existent review", null, null);

        _mockReviewService
            .Setup(s => s.UpdateReviewAsync(reviewId, dto, reviewerId, null))
            .ThrowsAsync(new ReviewNotFoundException(reviewId));

        // ACT
        var result = await _controller.UpdateReview(reviewId, dto, reviewerId, null);

        // ASSERT
        var notFoundResult = result as NotFoundObjectResult;
        Assert.That(notFoundResult, Is.Not.Null);
        Assert.That(notFoundResult!.StatusCode, Is.EqualTo(404));
        Assert.That(notFoundResult.Value?.ToString(), Does.Contain(reviewId.ToString()));
    }

    [Test]
    public async Task UpdateReview_ShouldReturnForbidden_WhenUnauthorized()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var dto = new UpdateReviewDto(5, "Trying to edit someone else's review", null, null);

        _mockReviewService
            .Setup(s => s.UpdateReviewAsync(reviewId, dto, reviewerId, null))
            .ThrowsAsync(new UnauthorizedReviewAccessException(reviewId));

        // ACT
        var result = await _controller.UpdateReview(reviewId, dto, reviewerId, null);

        // ASSERT
        var forbiddenResult = result as ObjectResult;
        Assert.That(forbiddenResult, Is.Not.Null);
        Assert.That(forbiddenResult!.StatusCode, Is.EqualTo(403));
        Assert.That(forbiddenResult.Value?.ToString(), Does.Contain("Unauthorized access"));
    }

    [Test]
    public async Task UpdateReview_ShouldReturnBadRequest_WhenValidationFails()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var dto = new UpdateReviewDto(
            StarRating: 6, // Invalid
            ReviewBody: null,
            PhotoUrls: null,
            ReviewAsAnon: null
        );

        _mockReviewService
            .Setup(s => s.UpdateReviewAsync(reviewId, dto, reviewerId, null))
            .ThrowsAsync(new ArgumentException("Star rating must be between 1 and 5."));

        // ACT
        var result = await _controller.UpdateReview(reviewId, dto, reviewerId, null);

        // ASSERT
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult, Is.Not.Null);
        Assert.That(badRequestResult!.StatusCode, Is.EqualTo(400));
        Assert.That(badRequestResult.Value?.ToString(), Does.Contain("Star rating"));
    }

    [Test]
    public async Task UpdateReview_ShouldReturnInternalServerError_WhenUpdateFails()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var dto = new UpdateReviewDto(5, "Updated review content", null, null);

        _mockReviewService
            .Setup(s => s.UpdateReviewAsync(reviewId, dto, reviewerId, null))
            .ThrowsAsync(new ReviewCreationFailedException("Failed to update review record."));

        // ACT
        var result = await _controller.UpdateReview(reviewId, dto, reviewerId, null);

        // ASSERT
        var errorResult = result as ObjectResult;
        Assert.That(errorResult, Is.Not.Null);
        Assert.That(errorResult!.StatusCode, Is.EqualTo(500));
        Assert.That(errorResult.Value?.ToString(), Does.Contain("Failed to update review record"));
    }

    [Test]
    public async Task UpdateReview_ShouldReturnInternalServerError_WhenUnexpectedErrorOccurs()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var dto = new UpdateReviewDto(5, "Updated review content", null, null);

        _mockReviewService
            .Setup(s => s.UpdateReviewAsync(reviewId, dto, reviewerId, null))
            .ThrowsAsync(new Exception("Unexpected failure"));

        // ACT
        var result = await _controller.UpdateReview(reviewId, dto, reviewerId, null);

        // ASSERT
        var errorResult = result as ObjectResult;
        Assert.That(errorResult, Is.Not.Null);
        Assert.That(errorResult!.StatusCode, Is.EqualTo(500));
        Assert.That(errorResult.Value?.ToString(), Does.Contain("Internal server error occurred"));
    }

    [Test]
    public async Task UpdateReview_ShouldReturnBadRequest_WhenModelStateInvalid()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var dto = new UpdateReviewDto(null, null, null, null);

        _controller.ModelState.AddModelError("dto", "At least one field must be provided");

        // ACT
        var result = await _controller.UpdateReview(reviewId, dto, reviewerId, null);

        // ASSERT
        var badRequestResult = result as BadRequestObjectResult;
        Assert.That(badRequestResult, Is.Not.Null);
        Assert.That(badRequestResult!.StatusCode, Is.EqualTo(400));
    }
}