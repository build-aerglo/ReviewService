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
        // ✅ CRITICAL: Enable Dapper snake_case mapping for tests
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        // Load configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        _configuration = builder.Build();
        _connectionString = _configuration.GetConnectionString("PostgresConnection")
                            ?? throw new InvalidOperationException("Missing connection string in appsettings.json");

        _repository = new ReviewRepository(_configuration);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Ensure the review table exists
        var tableExists = await conn.ExecuteScalarAsync<bool>(@"
            SELECT EXISTS (
                SELECT FROM information_schema.tables WHERE table_name = 'review'
            );
        ");

        if (!tableExists)
            Assert.Fail("❌ Table 'review' does not exist. Run migrations before running tests.");
    }

    [SetUp]
    public async Task Setup()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync("DELETE FROM review;");
        await conn.ExecuteAsync("DELETE FROM business;");
        await conn.ExecuteAsync("DELETE FROM location;");
        await conn.ExecuteAsync("DELETE FROM users;");
    }

    // insert dummy business for FK constraint
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

    // insert dummy location for FK constraint
    private async Task<Guid> InsertDummyLocationAsync()
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(@"
            INSERT INTO location (id, name, created_at)
            VALUES (@Id, 'Test Location', NOW());
        ", new { Id = id });
        return id;
    }

    //  insert dummy user for FK constraint
    private async Task<Guid> InsertDummyUserAsync()
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(@"
            INSERT INTO users (id, username, email, user_type)
            VALUES (@Id, @Username, @Email, @UserType);
        ", new { 
            Id = id, 
            Username = "testuser",
            Email = "testuser@example.com",
            UserType = "end_user"
        });
        return id;
    }

    [Test]
    public async Task AddAsync_ShouldInsertReview_AndGetById_ShouldReturnReview()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();

        var review = new Review(
            businessId: businessId,
            locationId: null,
            reviewerId: null,
            email: "test@example.com",
            starRating: 5,
            reviewBody: "Excellent service! Very satisfied with the experience and quality of work.",
            photoUrls: null,
            reviewAsAnon: false
        );

        // Act
        await _repository.AddAsync(review);
        var fetched = await _repository.GetByIdAsync(review.Id);

        // Assert
        Assert.That(fetched, Is.Not.Null);
        Assert.That(fetched!.BusinessId, Is.EqualTo(businessId));
        Assert.That(fetched.Email, Is.EqualTo("test@example.com"));
        Assert.That(fetched.StarRating, Is.EqualTo(5));
        Assert.That(fetched.ReviewBody, Does.Contain("Excellent service"));
    }

    [Test]
    public async Task GetByIdAsync_ShouldReturnNull_WhenReviewDoesNotExist()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetByBusinessIdAsync_ShouldReturnReviews_WhenReviewsExist()
    {
        var businessId = await InsertDummyBusinessAsync();

        var review1 = new Review(
            businessId,
            null,
            null,
            "user1@example.com",
            5,
            "Excellent service! Highly recommend this business to everyone.",
            null,
            false
        );

        var review2 = new Review(
            businessId,
            null,
            null,
            "guest@example.com",
            4,
            "Good service overall. Would definitely come back again.",
            null,
            true
        );

        await _repository.AddAsync(review1);
        await _repository.AddAsync(review2);

        var reviews = (await _repository.GetByBusinessIdAsync(businessId)).ToList();

        Assert.That(reviews.Count, Is.EqualTo(2));
        Assert.That(reviews.Any(r => r.Email == "user1@example.com"), Is.True);
        Assert.That(reviews.Any(r => r.StarRating == 4), Is.True);
    }

    [Test]
    public async Task GetByBusinessIdAsync_ShouldReturnEmpty_WhenNoReviewsExist()
    {
        var businessId = await InsertDummyBusinessAsync();
        var reviews = (await _repository.GetByBusinessIdAsync(businessId)).ToList();
        Assert.That(reviews, Is.Empty);
    }

    [Test]
    public async Task AddAsync_ShouldSupportAllFeatures()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();
        var locationId = await InsertDummyLocationAsync();
        var reviewerId = await InsertDummyUserAsync();
        var photoUrls = new[] { "photo1.jpg", "photo2.jpg", "photo3.jpg" };

        var review = new Review(
            businessId: businessId,
            locationId: locationId,
            reviewerId: reviewerId,
            email: null,
            starRating: 5,
            reviewBody: "Amazing experience! Added some photos for reference purposes.",
            photoUrls: photoUrls,
            reviewAsAnon: false
        );

        // Act
        await _repository.AddAsync(review);
        var fetched = await _repository.GetByIdAsync(review.Id);

        // Assert
        Assert.That(fetched, Is.Not.Null);
        Assert.That(fetched!.LocationId, Is.EqualTo(locationId));
        Assert.That(fetched.ReviewerId, Is.EqualTo(reviewerId));
        Assert.That(fetched.PhotoUrls, Is.Not.Null);
        Assert.That(fetched.PhotoUrls!.Length, Is.EqualTo(3));
    }

    [Test]
    public async Task UpdateAsync_ShouldUpdateReview_WhenReviewExists()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();
        var review = new Review(
            businessId: businessId,
            locationId: null,
            reviewerId: null,
            email: "update@example.com",
            starRating: 3,
            reviewBody: "Original review content that will be updated later.",
            photoUrls: null,
            reviewAsAnon: false
        );

        await _repository.AddAsync(review);

        // Act - Update the review
        review.Update(
            starRating: 5,
            reviewBody: "Updated review content after reconsideration.",
            photoUrls: new[] { "new-photo.jpg" },
            reviewAsAnon: true
        );
        await _repository.UpdateAsync(review);

        // Assert
        var updated = await _repository.GetByIdAsync(review.Id);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.StarRating, Is.EqualTo(5));
        Assert.That(updated.ReviewBody, Does.Contain("Updated review content"));
        Assert.That(updated.PhotoUrls, Is.Not.Null);
        Assert.That(updated.PhotoUrls!.Length, Is.EqualTo(1));
        Assert.That(updated.ReviewAsAnon, Is.True);
    }
    
    [Test]
    public async Task DeleteAsync_ShouldRemoveReview_WhenReviewExists()
    {
        // Arrange
        var businessId = await InsertDummyBusinessAsync();
        var review = new Review(
            businessId: businessId,
            locationId: null,
            reviewerId: null,
            email: "delete@example.com",
            starRating: 4,
            reviewBody: "Review that will be deleted permanently from database.",
            photoUrls: null,
            reviewAsAnon: false
        );

        await _repository.AddAsync(review);

        // Verify it exists
        var exists = await _repository.GetByIdAsync(review.Id);
        Assert.That(exists, Is.Not.Null);

        // Act
        await _repository.DeleteAsync(review.Id);

        // Assert
        var deleted = await _repository.GetByIdAsync(review.Id);
        Assert.That(deleted, Is.Null);
    }
}