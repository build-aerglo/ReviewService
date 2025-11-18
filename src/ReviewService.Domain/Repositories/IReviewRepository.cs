using ReviewService.Domain.Entities;


namespace ReviewService.Domain.Repositories;

public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(Guid id);
    Task<IEnumerable<Review>> GetByBusinessIdAsync(Guid businessId, string? status = null);
    Task AddAsync(Review review);
    Task UpdateAsync(Review review);
    Task DeleteAsync(Guid id);

    //  Internal query methods for Compliance Service
    Task<bool> HasRecentReviewAsync(
        Guid businessId, 
        Guid? reviewerId, 
        string? email,
        string? ipAddress, 
        string? deviceId, 
        TimeSpan timeWindow);

    Task<int> GetReviewCountAsync(
        Guid? reviewerId, 
        string? email,
        string? ipAddress, 
        string? deviceId, 
        TimeSpan timeWindow);

    Task<bool> HasReviewInCategoryAsync(
        Guid? reviewerId, 
        string? email,
        string? ipAddress, 
        string? deviceId, 
        string category, 
        TimeSpan timeWindow);

    Task<ReviewSpikeStats> GetReviewStatsAsync(Guid businessId, TimeSpan timeWindow);
}

public record ReviewSpikeStats(
    int TotalReviews,
    int PositiveCount,
    int NegativeCount,
    double ImbalanceRatio
);
