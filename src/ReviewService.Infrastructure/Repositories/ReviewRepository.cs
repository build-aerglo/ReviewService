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
        // ✅ Explicit column mapping ensures proper snake_case -> PascalCase conversion
        const string sql = @"
            SELECT 
                id,
                business_id,
                location_id,
                reviewer_id,
                email,
                star_rating,
                review_body,
                photo_urls,
                review_as_anon,
                created_at,
                updated_at
            FROM review 
            WHERE id = @Id;";
        
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Review>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Review>> GetByBusinessIdAsync(Guid businessId)
    {
        const string sql = @"
            SELECT 
                id,
                business_id,
                location_id,
                reviewer_id,
                email,
                star_rating,
                review_body,
                photo_urls,
                review_as_anon,
                created_at,
                updated_at
            FROM review 
            WHERE business_id = @BusinessId 
            ORDER BY created_at DESC;";
        
        using var conn = CreateConnection();
        return await conn.QueryAsync<Review>(sql, new { BusinessId = businessId });
    }

    public async Task AddAsync(Review review)
    {
        const string sql = @"
            INSERT INTO review (id, business_id, location_id, reviewer_id, email, star_rating, 
                               review_body, photo_urls, review_as_anon, created_at, updated_at)
            VALUES (@Id, @BusinessId, @LocationId, @ReviewerId, @Email, @StarRating, 
                    @ReviewBody, @PhotoUrls, @ReviewAsAnon, @CreatedAt, @UpdatedAt);";

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
                updated_at = @UpdatedAt
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
}