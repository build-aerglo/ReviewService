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
    public async Task CreateReviewAsync_ShouldReturnResponseWithPendingStatus_WhenSuccessful()
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
            .ReturnsAsync((Guid id) => new Review(
                businessId, null, reviewerId, null, 5,
                "Excellent service! Very satisfied with the experience.",
                new[] { "https://example.com/photo1.jpg" }, false,
                "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0"));

        // ACT
        var result = await _service.CreateReviewAsync(dto, "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0");

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.BusinessId, Is.EqualTo(businessId));
            Assert.That(result.ReviewerId, Is.EqualTo(reviewerId));
            Assert.That(result.StarRating, Is.EqualTo(5));
            Assert.That(result.Status, Is.EqualTo("PENDING")); // ✅ NEW: Check status
            Assert.That(result.ValidatedAt, Is.Null); // ✅ NEW: Not yet validated
        });

        _mockBusinessServiceClient.Verify(c => c.BusinessExistsAsync(businessId), Times.Once);
        _mockReviewRepository.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Once);
    }

    [Test]
    public async Task CreateReviewAsync_GuestUser_ShouldSucceed()
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
            .ReturnsAsync((Guid id) => new Review(
                businessId, null, null, "guest@example.com", 4,
                "Good service overall. Would recommend to others.",
                null, true,
                null, null, null, null));

        // ACT
        var result = await _service.CreateReviewAsync(dto);

        // ASSERT
        Assert.Multiple(() =>
        {
            Assert.That(result.Email, Is.EqualTo("guest@example.com"));
            Assert.That(result.ReviewAsAnon, Is.True);
            Assert.That(result.Status, Is.EqualTo("PENDING"));
        });
    }

    [Test]
    public void CreateReviewAsync_ShouldThrow_WhenBusinessDoesNotExist()
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
    public async Task GetReviewByIdAsync_ShouldReturnReviewWithStatus()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var review = new Review(
            businessId, null, null, "test@example.com", 4,
            "Good service and friendly staff overall.",
            null, false,
            "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0");

        review.UpdateValidationStatus("APPROVED", "{}");

        _mockReviewRepository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(review);

        // ACT
        var result = await _service.GetReviewByIdAsync(reviewId);

        // ASSERT
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Status, Is.EqualTo("APPROVED"));
        Assert.That(result.ValidatedAt, Is.Not.Null);
    }

    [Test]
    public async Task GetReviewsByBusinessIdAsync_ShouldReturnOnlyApprovedReviews()
    {
        // ARRANGE
        var businessId = Guid.NewGuid();
        var approvedReview = new Review(
            businessId, null, null, "user1@example.com", 5,
            "Excellent! Highly recommend this place.",
            null, false, null, null, null, null);
        approvedReview.UpdateValidationStatus("APPROVED", "{}");

        var reviews = new List<Review> { approvedReview };

        _mockReviewRepository
            .Setup(r => r.GetByBusinessIdAsync(businessId, "APPROVED"))
            .ReturnsAsync(reviews);

        // ACT
        var result = (await _service.GetReviewsByBusinessIdAsync(businessId)).ToList();

        // ASSERT
        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].Status, Is.EqualTo("APPROVED"));
    }

    [Test]
    public async Task GetReviewStatusAsync_ShouldReturnStatus_WhenEmailMatches()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var email = "test@example.com";

        var review = new Review(
            businessId, null, null, email, 5,
            "Great service! Very satisfied with the experience overall.",
            null, false, null, null, null, null);

        review.UpdateValidationStatus("APPROVED", "{\"isValid\":true,\"errors\":[],\"warnings\":[]}");

        _mockReviewRepository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(review);

        // ACT
        var result = await _service.GetReviewStatusAsync(reviewId, email);

        // ASSERT
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Status, Is.EqualTo("APPROVED"));
        Assert.That(result.ValidationResult, Is.Not.Null);
    }

    [Test]
    public async Task GetReviewStatusAsync_ShouldReturnNull_WhenEmailDoesNotMatch()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var businessId = Guid.NewGuid();

        var review = new Review(
            businessId, null, null, "original@example.com", 5,
            "Great service! Very satisfied with the experience overall.", 
            null, false, null, null, null, null);

        _mockReviewRepository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(review);

        // ACT
        var result = await _service.GetReviewStatusAsync(reviewId, "wrong@example.com");

        // ASSERT
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task UpdateReviewAsync_ShouldSucceed_WhenAuthorized()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var existingReview = new Review(
            businessId, null, reviewerId, null, 4,
            "Good service and friendly staff overall.",
            null, false, null, null, null, null);

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
        Assert.That(result.StarRating, Is.EqualTo(5));
        Assert.That(result.ReviewBody, Does.Contain("Updated"));
    }

    [Test]
    public async Task DeleteReviewAsync_ShouldSucceed_WhenAuthorized()
    {
        // ARRANGE
        var reviewId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var existingReview = new Review(
            businessId, null, reviewerId, null, 4,
            "Review to be deleted.",
            null, false, null, null, null, null);

        _mockReviewRepository.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(existingReview);
        _mockReviewRepository.Setup(r => r.DeleteAsync(reviewId)).Returns(Task.CompletedTask);

        // ACT
        await _service.DeleteReviewAsync(reviewId, reviewerId, null);

        // ASSERT
        _mockReviewRepository.Verify(r => r.DeleteAsync(reviewId), Times.Once);
    }
}