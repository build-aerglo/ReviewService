using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ReviewService.Domain.Entities;
using ReviewService.Domain.Repositories;

namespace ReviewService.Infrastructure.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly string _connectionString;

    public ReviewRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("PostgresConnection")!;
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<Review?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT 
                id, business_id, location_id, reviewer_id, email, star_rating,
                review_body, photo_urls, review_as_anon, created_at, updated_at,
                status, ip_address, device_id, geolocation, user_agent,
                validation_result, validated_at
            FROM review 
            WHERE id = @Id;";
        
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Review>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Review>> GetByBusinessIdAsync(Guid businessId, string? status = null)
    {
        // Default to showing only APPROVED reviews if no status specified
        var statusFilter = status ?? ReviewStatus.Approved;
        
        const string sql = @"
            SELECT 
                id, business_id, location_id, reviewer_id, email, star_rating,
                review_body, photo_urls, review_as_anon, created_at, updated_at,
                status, ip_address, device_id, geolocation, user_agent,
                validation_result, validated_at
            FROM review 
            WHERE business_id = @BusinessId 
            AND status = @Status
            ORDER BY created_at DESC;";
        
        using var conn = CreateConnection();
        return await conn.QueryAsync<Review>(sql, new { BusinessId = businessId, Status = statusFilter });
    }

    public async Task AddAsync(Review review)
    {
        const string sql = @"
            INSERT INTO review (
                id, business_id, location_id, reviewer_id, email, star_rating, 
                review_body, photo_urls, review_as_anon, created_at, updated_at,
                status, ip_address, device_id, geolocation, user_agent
            )
            VALUES (
                @Id, @BusinessId, @LocationId, @ReviewerId, @Email, @StarRating, 
                @ReviewBody, @PhotoUrls, @ReviewAsAnon, @CreatedAt, @UpdatedAt,
                @Status, @IpAddress, @DeviceId, @Geolocation, @UserAgent
            );";

        using var conn = CreateConnection();
        await conn.ExecuteAsync(sql, review);
    }

    public async Task UpdateAsync(Review review)
    {
        
        const string sql = @"
            UPDATE review
            SET star_rating = @StarRating,
                review_body = @ReviewBody,
                photo_urls = @PhotoUrls,
                review_as_anon = @ReviewAsAnon,
                updated_at = @UpdatedAt,
                status = @Status,
                validation_result = @ValidationResult::jsonb,
                validated_at = @ValidatedAt
            WHERE id = @Id;";

        using var conn = CreateConnection();
        await conn.ExecuteAsync(sql, review);
    }

    public async Task DeleteAsync(Guid id)
    {
        const string sql = "DELETE FROM review WHERE id = @Id;";
        using var conn = CreateConnection();
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    // ========================================
    //  Internal Query Methods
    // ========================================

    public async Task<bool> HasRecentReviewAsync(
    Guid businessId,
    Guid? reviewerId,
    string? email,
    string? ipAddress,
    string? deviceId,
    TimeSpan timeWindow)
{
    var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
    
    const string sql = @"
        SELECT EXISTS(
            SELECT 1 FROM review 
            WHERE business_id = @BusinessId
              AND created_at >= @CutoffTime
              AND status IN ('APPROVED', 'REJECTED', 'FLAGGED')  -- ✅ Exclude PENDING
              AND (
                  (reviewer_id IS NOT NULL AND reviewer_id = @ReviewerId) OR
                  (email IS NOT NULL AND email = @Email) OR
                  (ip_address IS NOT NULL AND ip_address = @IpAddress) OR
                  (device_id IS NOT NULL AND device_id = @DeviceId)
              )
        );";

    using var conn = CreateConnection();
    return await conn.ExecuteScalarAsync<bool>(sql, new
    {
        BusinessId = businessId,
        CutoffTime = cutoffTime,
        ReviewerId = reviewerId,
        Email = email,
        IpAddress = ipAddress,
        DeviceId = deviceId
    });
}

public async Task<int> GetReviewCountAsync(
    Guid? reviewerId,
    string? email,
    string? ipAddress,
    string? deviceId,
    TimeSpan timeWindow)
{
    var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
    
    const string sql = @"
        SELECT COUNT(*) FROM review 
        WHERE created_at >= @CutoffTime
          AND status IN ('APPROVED', 'REJECTED', 'FLAGGED')  -- ✅ Exclude PENDING
          AND (
              (reviewer_id IS NOT NULL AND reviewer_id = @ReviewerId) OR
              (email IS NOT NULL AND email = @Email) OR
              (ip_address IS NOT NULL AND ip_address = @IpAddress) OR
              (device_id IS NOT NULL AND device_id = @DeviceId)
          );";

    using var conn = CreateConnection();
    return await conn.ExecuteScalarAsync<int>(sql, new
    {
        CutoffTime = cutoffTime,
        ReviewerId = reviewerId,
        Email = email,
        IpAddress = ipAddress,
        DeviceId = deviceId
    });
}

public async Task<bool> HasReviewInCategoryAsync(
    Guid? reviewerId,
    string? email,
    string? ipAddress,
    string? deviceId,
    string category,
    TimeSpan timeWindow)
{
    var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
    
    const string sql = @"
        SELECT EXISTS(
            SELECT 1 FROM review r
            JOIN business b ON r.business_id = b.id
            JOIN business_category bc ON b.id = bc.business_id
            JOIN category c ON bc.category_id = c.id
            WHERE c.name = @Category
              AND r.created_at >= @CutoffTime
              AND r.status IN ('APPROVED', 'REJECTED', 'FLAGGED')  -- ✅ Exclude PENDING
              AND (
                  (r.reviewer_id IS NOT NULL AND r.reviewer_id = @ReviewerId) OR
                  (r.email IS NOT NULL AND r.email = @Email) OR
                  (r.ip_address IS NOT NULL AND r.ip_address = @IpAddress) OR
                  (r.device_id IS NOT NULL AND r.device_id = @DeviceId)
              )
        );";

    using var conn = CreateConnection();
    return await conn.ExecuteScalarAsync<bool>(sql, new
    {
        Category = category,
        CutoffTime = cutoffTime,
        ReviewerId = reviewerId,
        Email = email,
        IpAddress = ipAddress,
        DeviceId = deviceId
    });
}

public async Task<ReviewSpikeStats> GetReviewStatsAsync(Guid businessId, TimeSpan timeWindow)
{
    var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
    
    const string sql = @"
        SELECT 
            COUNT(*) as TotalReviews,
            SUM(CASE WHEN star_rating >= 4 THEN 1 ELSE 0 END) as PositiveCount,
            SUM(CASE WHEN star_rating <= 2 THEN 1 ELSE 0 END) as NegativeCount
        FROM review 
        WHERE business_id = @BusinessId 
          AND created_at >= @CutoffTime
          AND status IN ('APPROVED', 'REJECTED', 'FLAGGED');  -- ✅ Exclude PENDING";

    using var conn = CreateConnection();
    var result = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new
    {
        BusinessId = businessId,
        CutoffTime = cutoffTime
    });

    var totalReviews = (int)(result?.TotalReviews ?? 0);
    var positiveCount = (int)(result?.PositiveCount ?? 0);
    var negativeCount = (int)(result?.NegativeCount ?? 0);

    // Calculate imbalance ratio
    double imbalanceRatio = 0;
    if (totalReviews > 0)
    {
        var positiveRatio = (double)positiveCount / totalReviews;
        var negativeRatio = (double)negativeCount / totalReviews;
        imbalanceRatio = Math.Abs(positiveRatio - negativeRatio);
    }

    return new ReviewSpikeStats(totalReviews, positiveCount, negativeCount, imbalanceRatio);
}
}