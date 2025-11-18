using ReviewService.Application.DTOs;

namespace ReviewService.Application.Services;

public interface IReviewService
{
    Task<ReviewResponseDto> CreateReviewAsync(
        CreateReviewDto dto,
        string? ipAddress = null,
        string? deviceId = null,
        string? geolocation = null,
        string? userAgent = null);
    
    Task<ReviewResponseDto?> GetReviewByIdAsync(Guid id);
    Task<IEnumerable<ReviewResponseDto>> GetReviewsByBusinessIdAsync(Guid businessId);
    Task<ReviewStatusDto?> GetReviewStatusAsync(Guid reviewId, string email);
    Task<ReviewResponseDto> UpdateReviewAsync(Guid id, UpdateReviewDto dto, Guid? reviewerId, string? email);
    Task DeleteReviewAsync(Guid id, Guid? reviewerId, string? email);
}