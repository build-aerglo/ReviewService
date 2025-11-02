using Moq;
using ReviewService.Application.DTOs;
using ReviewService.Application.Interfaces;
using ReviewService.Domain.Entities;
using ReviewService.Domain.Exceptions;
using ReviewService.Domain.Repositories;

namespace ReviewService.Application.Tests.Services;

[TestFixture]
public class ReviewServiceTests
{
    private Mock<IReviewRepository> _mockReviewRepository = null!;
    private Mock<IBusinessServiceClient> _mockBusinessServiceClient = null!;
    private Mock<IUserServiceClient> _mockUserServiceClient = null!;
    private Application.Services.ReviewService _service = null!;

    [SetUp]
    public void Setup()
    {
        _mockReviewRepository = new Mock<IReviewRepository>();
        _mockBusinessServiceClient = new Mock<IBusinessServiceClient>();
        _mockUserServiceClient = new Mock<IUserServiceClient>();

        _service = new Application.Services.ReviewService(
            _mockReviewRepository.Object,
            _mockBusinessServiceClient.Object,
            _mockUserServiceClient.Object
        );
    }

    [Test]
    public async Task CreateReview_ShouldReturnResponse_WhenSuccessful_RegisteredUser()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var dto = new CreateReviewDto(
            BusinessId: businessId,
            LocationId: null,
            ReviewerId: reviewerId,
            Email: null,
            StarRating: 5,
            ReviewBody: "Excellent service! Very satisfied with the experience.",
            PhotoUrls: new[] { "https://example.com/photo1.jpg" },
            ReviewAsAnon: false
        );

        _mockBusinessServiceClient.Setup(c => c.BusinessExistsAsync(businessId)).ReturnsAsync(true);

        _mockReviewRepository.Setup(r => r.AddAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);
        _mockReviewRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Review(businessId, null, reviewerId, null, 5, 
                "Excellent service! Very satisfied with the experience.", 
                new[] { "https://example.com/photo1.jpg" }, false));

        // ACT
        var result = await _service.CreateReviewAsync(dto);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.BusinessId, Is.EqualTo(businessId));
            Assert.That(result.ReviewerId, Is.EqualTo(reviewerId));
            Assert.That(result.StarRating, Is.EqualTo(5));
            Assert.That(result.ReviewBody, Is.EqualTo("Excellent service! Very satisfied with the experience."));
            Assert.That(result.ReviewAsAnon, Is.False);
        });

        _mockBusinessServiceClient.Verify(c => c.BusinessExistsAsync(businessId), Times.Once);
        _mockReviewRepository.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Once);
    }

    [Test]
    public async Task CreateReview_ShouldReturnResponse_WhenSuccessful_GuestUser()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        var dto = new CreateReviewDto(
            BusinessId: businessId,
            LocationId: null,
            ReviewerId: null,
            Email: "guest@example.com",
            StarRating: 4,
            ReviewBody: "Good service overall. Would recommend to others.",
            PhotoUrls: null,
            ReviewAsAnon: true
        );

        _mockBusinessServiceClient.Setup(c => c.BusinessExistsAsync(businessId)).ReturnsAsync(true);

        _mockReviewRepository.Setup(r => r.AddAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);
        _mockReviewRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Review(businessId, null, null, "guest@example.com", 4,
                "Good service overall. Would recommend to others.", null, true));

        // ACT
        var result = await _service.CreateReviewAsync(dto);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.BusinessId, Is.EqualTo(businessId));
            Assert.That(result.ReviewerId, Is.Null);
            Assert.That(result.Email, Is.EqualTo("guest@example.com"));
            Assert.That(result.StarRating, Is.EqualTo(4));
            Assert.That(result.ReviewAsAnon, Is.True);
        });

        _mockBusinessServiceClient.Verify(c => c.BusinessExistsAsync(businessId), Times.Once);
        _mockUserServiceClient.Verify(c => c.UserExistsAsync(It.IsAny<Guid>()), Times.Never);
        _mockReviewRepository.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Once);
    }

    [Test]
    public void CreateReview_ShouldThrow_WhenBusinessDoesNotExist()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        var dto = new CreateReviewDto(
            BusinessId: businessId,
            LocationId: null,
            ReviewerId: Guid.NewGuid(),
            Email: null,
            StarRating: 5,
            ReviewBody: "This should fail because business doesn't exist.",
            PhotoUrls: null,
            ReviewAsAnon: false
        );

        _mockBusinessServiceClient.Setup(c => c.BusinessExistsAsync(businessId)).ReturnsAsync(false);

        // ACT & ASSERT
        var ex = Assert.ThrowsAsync<BusinessNotFoundException>(
            async () => await _service.CreateReviewAsync(dto)
        );

        Assert.That(ex!.Message, Does.Contain(businessId.ToString()));
        _mockReviewRepository.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Test]
    public void CreateReview_ShouldThrow_WhenReviewSaveFails()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        var dto = new CreateReviewDto(
            BusinessId: businessId,
            LocationId: null,
            ReviewerId: null,
            Email: "fail@example.com",
            StarRating: 5,
            ReviewBody: "This review should fail to save to database.",
            PhotoUrls: null,
            ReviewAsAnon: false
        );

        _mockBusinessServiceClient.Setup(c => c.BusinessExistsAsync(businessId)).ReturnsAsync(true);
        _mockReviewRepository.Setup(r => r.AddAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);
        _mockReviewRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Review?)null);

        // ACT & ASSERT
        var ex = Assert.ThrowsAsync<ReviewCreationFailedException>(
            async () => await _service.CreateReviewAsync(dto)
        );

        Assert.That(ex!.Message, Does.Contain("Failed to create review record."));
    }

    [Test]
    public async Task CreateReview_WithLocation_ShouldSucceed()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var dto = new CreateReviewDto(
            BusinessId: businessId,
            LocationId: locationId,
            ReviewerId: null,
            Email: "location@example.com",
            StarRating: 5,
            ReviewBody: "Great location! Easy to find and good parking.",
            PhotoUrls: null,
            ReviewAsAnon: false
        );

        _mockBusinessServiceClient.Setup(c => c.BusinessExistsAsync(businessId)).ReturnsAsync(true);

        _mockReviewRepository.Setup(r => r.AddAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);
        _mockReviewRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Review(businessId, locationId, null, "location@example.com", 5,
                "Great location! Easy to find and good parking.", null, false));

        // ACT
        var result = await _service.CreateReviewAsync(dto);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.LocationId, Is.EqualTo(locationId));
            Assert.That(result.Email, Is.EqualTo("location@example.com"));
        });
    }

    [Test]
    public async Task CreateReview_WithMultiplePhotos_ShouldSucceed()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        var photoUrls = new[] { "photo1.jpg", "photo2.jpg", "photo3.jpg" };
        var dto = new CreateReviewDto(
            BusinessId: businessId,
            LocationId: null,
            ReviewerId: null,
            Email: "photos@example.com",
            StarRating: 5,
            ReviewBody: "Amazing experience! Added some photos for reference.",
            PhotoUrls: photoUrls,
            ReviewAsAnon: false
        );

        _mockBusinessServiceClient.Setup(c => c.BusinessExistsAsync(businessId)).ReturnsAsync(true);
        _mockReviewRepository.Setup(r => r.AddAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);
        _mockReviewRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => new Review(businessId, null, null, "photos@example.com", 5,
                "Amazing experience! Added some photos for reference.", photoUrls, false));

        // ACT
        var result = await _service.CreateReviewAsync(dto);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.PhotoUrls, Is.Not.Null);
            Assert.That(result.PhotoUrls!.Length, Is.EqualTo(3));
            Assert.That(result.PhotoUrls, Is.EqualTo(photoUrls));
        });
    }

    [Test]
    public async Task GetReviewById_ShouldReturnReview_WhenExists()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var review = new Review(businessId, null, null, "test@example.com", 4,
            "Good service and friendly staff overall.",
            null, false);

        _mockReviewRepository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(review);

        // ACT
        var result = await _service.GetReviewByIdAsync(reviewId);

        // ASSERT
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.BusinessId, Is.EqualTo(businessId));
        Assert.That(result.StarRating, Is.EqualTo(4));
    }

    [Test]
    public async Task GetReviewById_ShouldReturnNull_WhenNotExists()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        _mockReviewRepository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync((Review?)null);

        // ACT
        var result = await _service.GetReviewByIdAsync(reviewId);

        // ASSERT
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetReviewsByBusinessId_ShouldReturnReviews_WhenExist()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        var reviews = new List<Review>
        {
            new Review(businessId, null, null, "user1@example.com", 5,
                "Excellent! Highly recommend this place.", null, false),
            new Review(businessId, null, null, "user2@example.com", 4,
                "Very good service. Will come back again.", null, true)
        };

        _mockReviewRepository.Setup(r => r.GetByBusinessIdAsync(businessId)).ReturnsAsync(reviews);

        // ACT
        var result = (await _service.GetReviewsByBusinessIdAsync(businessId)).ToList();

        // ASSERT
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].StarRating, Is.EqualTo(5));
        Assert.That(result[1].StarRating, Is.EqualTo(4));
    }

    [Test]
    public async Task GetReviewsByBusinessId_ShouldReturnEmpty_WhenNoReviews()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        _mockReviewRepository.Setup(r => r.GetByBusinessIdAsync(businessId))
            .ReturnsAsync(new List<Review>());

        // ACT
        var result = (await _service.GetReviewsByBusinessIdAsync(businessId)).ToList();

        // ASSERT
        Assert.That(result, Is.Empty);
    }

    // ======================================
    // UPDATE REVIEW TESTS
    // ======================================

    [Test]
    public async Task UpdateReview_ShouldSucceed_WhenRegisteredUserOwnsReview()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var existingReview = new Review(businessId, null, reviewerId, null, 4,
            "Good service and friendly staff overall.", null, false);

        var updateDto = new UpdateReviewDto(
            StarRating: 5,
            ReviewBody: "Updated: Excellent service! Changed my mind after follow-up.",
            PhotoUrls: new[] { "photo1.jpg" },
            ReviewAsAnon: true
        );

        _mockReviewRepository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(existingReview);
        _mockReviewRepository.Setup(r => r.UpdateAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);

        // ACT
        var result = await _service.UpdateReviewAsync(reviewId, updateDto, reviewerId, null);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.StarRating, Is.EqualTo(5));
            Assert.That(result.ReviewBody, Does.Contain("Updated: Excellent service!"));
            Assert.That(result.ReviewAsAnon, Is.True);
            Assert.That(result.PhotoUrls, Is.Not.Null);
            Assert.That(result.PhotoUrls!.Length, Is.EqualTo(1));
        });

        _mockReviewRepository.Verify(r => r.UpdateAsync(It.IsAny<Review>()), Times.Once);
    }

    [Test]
    public async Task UpdateReview_ShouldSucceed_WhenGuestUserOwnsReview()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var email = "guest@example.com";

        var existingReview = new Review(businessId, null, null, email, 3,
            "Decent service but could be better.", null, true);

        var updateDto = new UpdateReviewDto(
            StarRating: 4,
            ReviewBody: "Updated: Actually pretty good after trying again.",
            PhotoUrls: null,
            ReviewAsAnon: false
        );

        _mockReviewRepository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(existingReview);
        _mockReviewRepository.Setup(r => r.UpdateAsync(It.IsAny<Review>())).Returns(Task.CompletedTask);

        // ACT
        var result = await _service.UpdateReviewAsync(reviewId, updateDto, null, email);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.StarRating, Is.EqualTo(4));
            Assert.That(result.ReviewBody, Does.Contain("Updated: Actually pretty good"));
            Assert.That(result.ReviewAsAnon, Is.False);
        });

        _mockReviewRepository.Verify(r => r.UpdateAsync(It.IsAny<Review>()), Times.Once);
    }

    [Test]
    public void UpdateReview_ShouldThrow_WhenReviewNotFound()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var updateDto = new UpdateReviewDto(5, "Updated review", null, null);

        _mockReviewRepository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync((Review?)null);

        // ACT & ASSERT
        var ex = Assert.ThrowsAsync<ReviewNotFoundException>(
            async () => await _service.UpdateReviewAsync(reviewId, updateDto, Guid.NewGuid(), null)
        );

        Assert.That(ex!.Message, Does.Contain(reviewId.ToString()));
        _mockReviewRepository.Verify(r => r.UpdateAsync(It.IsAny<Review>()), Times.Never);
    }

    [Test]
    public void UpdateReview_ShouldThrow_WhenUnauthorized_DifferentReviewerId()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var originalReviewerId = Guid.NewGuid();
        var differentReviewerId = Guid.NewGuid();

        var existingReview = new Review(businessId, null, originalReviewerId, null, 4,
            "Good service and friendly staff overall.", null, false);

        var updateDto = new UpdateReviewDto(5, "Trying to edit someone else's review", null, null);

        _mockReviewRepository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(existingReview);

        // ACT & ASSERT
        var ex = Assert.ThrowsAsync<UnauthorizedReviewAccessException>(
            async () => await _service.UpdateReviewAsync(reviewId, updateDto, differentReviewerId, null)
        );

        Assert.That(ex!.Message, Does.Contain(reviewId.ToString()));
        _mockReviewRepository.Verify(r => r.UpdateAsync(It.IsAny<Review>()), Times.Never);
    }

    [Test]
    public void UpdateReview_ShouldThrow_WhenUnauthorized_DifferentEmail()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var originalEmail = "original@example.com";
        var differentEmail = "hacker@example.com";

        var existingReview = new Review(businessId, null, null, originalEmail, 4,
            "Good service and friendly staff overall.", null, true);

        var updateDto = new UpdateReviewDto(5, "Trying to edit someone else's review", null, null);

        _mockReviewRepository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(existingReview);

        // ACT & ASSERT
        var ex = Assert.ThrowsAsync<UnauthorizedReviewAccessException>(
            async () => await _service.UpdateReviewAsync(reviewId, updateDto, null, differentEmail)
        );

        Assert.That(ex!.Message, Does.Contain(reviewId.ToString()));
        _mockReviewRepository.Verify(r => r.UpdateAsync(It.IsAny<Review>()), Times.Never);
    }

    [Test]
    public void UpdateReview_ShouldThrow_WhenInvalidStarRating()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var existingReview = new Review(businessId, null, reviewerId, null, 4,
            "Good service and friendly staff overall.", null, false);

        var updateDto = new UpdateReviewDto(
            StarRating: 6, // Invalid: > 5
            ReviewBody: null,
            PhotoUrls: null,
            ReviewAsAnon: null
        );

        _mockReviewRepository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(existingReview);

        // ACT & ASSERT
        var ex = Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.UpdateReviewAsync(reviewId, updateDto, reviewerId, null)
        );

        Assert.That(ex!.Message, Does.Contain("Star rating must be between 1 and 5"));
    }
}