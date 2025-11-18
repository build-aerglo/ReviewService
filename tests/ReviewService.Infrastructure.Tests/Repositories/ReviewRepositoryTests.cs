using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ReviewService.Domain.Entities;
using ReviewService.Infrastructure.Repositories;

namespace ReviewService.Infrastructure.Tests.Repositories;

[TestFixture]
public class ReviewRepositoryTests
{
    private ReviewRepository _repository = null!;
    private IConfiguration _configuration = null!;
    private string _connectionString = string.Empty;

    [OneTimeSetUp]
    public async Task GlobalSetup()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        _configuration = builder.Build();
        _connectionString = _configuration.GetConnectionString("PostgresConnection")
                            ?? throw new InvalidOperationException("Missing connection string");

        _repository = new ReviewRepository(_configuration);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var tableExists = await conn.ExecuteScalarAsync<bool>(@"
            SELECT EXISTS (
                SELECT FROM information_schema.tables WHERE table_name = 'review'
            );
        ");

        if (!tableExists)
            Assert.Fail("Table 'review' does not exist. Run migrations first.");
    }

    [SetUp]
    public async Task Setup()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync("DELETE FROM review;");
        await conn.ExecuteAsync("DELETE FROM business;");
    }

    private async Task<Guid> InsertDummyBusinessAsync()
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(@"
            INSERT INTO business (id, name, created_at)
            VALUES (@Id, 'Test Business', NOW());
        ", new { Id = id });
        return id;
    }

    [Test]
    public async Task AddAsync_ShouldInsertReviewWithPendingStatus()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();
        var review = new Review(
            businessId: businessId,
            locationId: null,
            reviewerId: null,
            email: "test@example.com",
            starRating: 5,
            reviewBody: "Excellent service! Very satisfied with everything.",
            photoUrls: null,
            reviewAsAnon: false,
            ipAddress: "192.168.1.1",
            deviceId: "device-123",
            geolocation: "Lagos, Lagos, Nigeria",
            userAgent: "Mozilla/5.0"
        );

        // Act
        await _repository.AddAsync(review);
        var fetched = await _repository.GetByIdAsync(review.Id);

        // Assert
        Assert.That(fetched, Is.Not.Null);
        Assert.That(fetched!.Status, Is.EqualTo("PENDING"));
        Assert.That(fetched.IpAddress, Is.EqualTo("192.168.1.1"));
        Assert.That(fetched.DeviceId, Is.EqualTo("device-123"));
        Assert.That(fetched.Geolocation, Is.EqualTo("Lagos, Lagos, Nigeria"));
        Assert.That(fetched.ValidatedAt, Is.Null);
    }

    [Test]
    public async Task UpdateAsync_ShouldUpdateValidationStatus()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();
        var review = new Review(
            businessId, null, null, "test@example.com", 5,
            "Great service! Very satisfied with everything here.", 
            null, false,
            "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0");

        await _repository.AddAsync(review);

        // Act - Update validation status
        review.UpdateValidationStatus("APPROVED", "{\"isValid\":true}");
        await _repository.UpdateAsync(review);

        // Assert
        var updated = await _repository.GetByIdAsync(review.Id);
        Assert.That(updated!.Status, Is.EqualTo("APPROVED"));
        Assert.That(updated.ValidatedAt, Is.Not.Null);
        Assert.That(updated.ValidationResult, Is.Not.Null);
    }

    // ========================================
    // ✅ NEW: VALIDATION QUERY TESTS
    // ========================================

    [Test]
    public async Task HasRecentReviewAsync_ShouldReturnTrue_WhenDuplicateExists()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();
        var email = "duplicate@example.com";

        var review = new Review(
            businessId, null, null, email, 5,
            "First review with sufficient length for validation.", // ✅ FIXED: 20+ chars
            null, false,
            "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0");

        await _repository.AddAsync(review);

        // Mark as APPROVED so it's counted in validation
        review.UpdateValidationStatus("APPROVED", "{}");
        await _repository.UpdateAsync(review);

        // Act
        var hasDuplicate = await _repository.HasRecentReviewAsync(
            businessId, null, email, "192.168.1.1", "device-123", TimeSpan.FromHours(72));

        // Assert
        Assert.That(hasDuplicate, Is.True);
    }

    [Test]
    public async Task HasRecentReviewAsync_ShouldReturnFalse_WhenNoDuplicate()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();

        // Act
        var hasDuplicate = await _repository.HasRecentReviewAsync(
            businessId, null, "new@example.com", "192.168.1.100", "device-999", TimeSpan.FromHours(72));

        // Assert
        Assert.That(hasDuplicate, Is.False);
    }

    [Test]
    public async Task HasRecentReviewAsync_ShouldIgnorePendingReviews()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();
        var email = "pending@example.com";

        var review = new Review(
            businessId, null, null, email, 5,
            "Pending review with sufficient length for testing.", // ✅ FIXED: 20+ chars
            null, false,
            "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0");

        await _repository.AddAsync(review);
        // Don't update status - leave as PENDING

        // Act
        var hasDuplicate = await _repository.HasRecentReviewAsync(
            businessId, null, email, "192.168.1.1", "device-123", TimeSpan.FromHours(72));

        // Assert
        Assert.That(hasDuplicate, Is.False); // Should ignore PENDING reviews
    }

    [Test]
    public async Task GetReviewCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();
        var email = "frequent@example.com";

        // Create 3 approved reviews
        for (int i = 0; i < 3; i++)
        {
            var review = new Review(
                businessId, null, null, email, 5,
                $"Review number {i + 1} with sufficient content for validation testing.",
                null, false,
                "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0");

            await _repository.AddAsync(review);
            review.UpdateValidationStatus("APPROVED", "{}");
            await _repository.UpdateAsync(review);
        }

        // Act
        var count = await _repository.GetReviewCountAsync(
            null, email, "192.168.1.1", "device-123", TimeSpan.FromHours(12));

        // Assert
        Assert.That(count, Is.EqualTo(3));
    }

    [Test]
    public async Task GetReviewCountAsync_ShouldIgnorePendingReviews()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();
        var email = "mixed@example.com";

        // 2 approved + 1 pending
        for (int i = 0; i < 2; i++)
        {
            var review = new Review(
                businessId, null, null, email, 5,
                $"Approved review {i + 1} with sufficient content for validation testing.", // ✅ FIXED: Added longer text
                null, false,
                "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0");

            await _repository.AddAsync(review);
            review.UpdateValidationStatus("APPROVED", "{}");
            await _repository.UpdateAsync(review);
        }

        var pendingReview = new Review(
            businessId, null, null, email, 5,
            "Pending review with sufficient length for testing purposes.", // ✅ FIXED: Added longer text
            null, false,
            "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0");
        await _repository.AddAsync(pendingReview);
        // Leave as PENDING

        // Act
        var count = await _repository.GetReviewCountAsync(
            null, email, "192.168.1.1", "device-123", TimeSpan.FromHours(12));

        // Assert
        Assert.That(count, Is.EqualTo(2)); // Only count approved, not pending
    }

    [Test]
    public async Task GetReviewStatsAsync_ShouldReturnCorrectStatistics()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();

        // Create 5 reviews: 3 positive (4-5 stars), 2 negative (1-2 stars)
        var ratings = new[] { 5, 5, 4, 2, 1 };
        foreach (var rating in ratings)
        {
            var review = new Review(
                businessId, null, null, $"user{rating}@example.com", rating,
                "Test review with sufficient length for validation testing purposes.",
                null, false,
                "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0");

            await _repository.AddAsync(review);
            review.UpdateValidationStatus("APPROVED", "{}");
            await _repository.UpdateAsync(review);
        
            // ✅ ADD THIS: Small delay to ensure created_at timestamps are within the time window
            await Task.Delay(10);
        }

        // Act
        // ✅ IMPORTANT: Use very large time window OR use TimeSpan.FromDays(365) to catch all reviews
        var stats = await _repository.GetReviewStatsAsync(businessId, TimeSpan.FromDays(365));

        // Assert
        Assert.That(stats.TotalReviews, Is.EqualTo(5));
        Assert.That(stats.PositiveCount, Is.EqualTo(3)); // 5, 5, 4
        Assert.That(stats.NegativeCount, Is.EqualTo(2)); // 2, 1
        Assert.That(stats.ImbalanceRatio, Is.GreaterThan(0));
    }


    [Test]
    public async Task GetReviewStatsAsync_ShouldIgnorePendingReviews()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();

        // 2 approved + 1 pending
        for (int i = 0; i < 2; i++)
        {
            var review = new Review(
                businessId, null, null, $"user{i}@example.com", 5,
                "Approved review with sufficient length for validation testing purposes.",
                null, false,
                "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0");

            await _repository.AddAsync(review);
            review.UpdateValidationStatus("APPROVED", "{}");
            await _repository.UpdateAsync(review);
        
            // ✅ ADD THIS: Small delay
            await Task.Delay(10);
        }

        var pendingReview = new Review(
            businessId, null, null, "pending@example.com", 5,
            "Pending review with sufficient length for testing purposes.",
            null, false,
            "192.168.1.1", "device-123", "Lagos, Lagos, Nigeria", "Mozilla/5.0");
        await _repository.AddAsync(pendingReview);

        // Act
        // ✅ IMPORTANT: Use very large time window to catch all reviews
        var stats = await _repository.GetReviewStatsAsync(businessId, TimeSpan.FromDays(365));

        // Assert
        Assert.That(stats.TotalReviews, Is.EqualTo(2)); // Only approved reviews
    }


    [Test]
    public async Task GetByBusinessIdAsync_ShouldFilterByStatus()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();

        // Create reviews with different statuses
        var approved = new Review(
            businessId, null, null, "approved@example.com", 5,
            "Approved review with sufficient length for testing.",
            null, false, null, null, null, null);
        await _repository.AddAsync(approved);
        approved.UpdateValidationStatus("APPROVED", "{}");
        await _repository.UpdateAsync(approved);

        var rejected = new Review(
            businessId, null, null, "rejected@example.com", 5,
            "Rejected review with sufficient length for testing.",
            null, false, null, null, null, null);
        await _repository.AddAsync(rejected);
        rejected.UpdateValidationStatus("REJECTED", "{}");
        await _repository.UpdateAsync(rejected);

        var pending = new Review(
            businessId, null, null, "pending@example.com", 5,
            "Pending review with sufficient length for testing.",
            null, false, null, null, null, null);
        await _repository.AddAsync(pending);

        // Act
        var reviews = (await _repository.GetByBusinessIdAsync(businessId, "APPROVED")).ToList();

        // Assert
        Assert.That(reviews.Count, Is.EqualTo(1));
        Assert.That(reviews[0].Status, Is.EqualTo("APPROVED"));
    }
}