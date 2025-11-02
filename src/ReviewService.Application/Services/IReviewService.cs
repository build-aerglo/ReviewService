using ReviewService.Application.DTOs;

namespace ReviewService.Application.Services;

public interface IReviewService
{
    Task<ReviewResponseDto> CreateReviewAsync(CreateReviewDto dto);
    Task<ReviewResponseDto?> GetReviewByIdAsync(Guid id);
    Task<IEnumerable<ReviewResponseDto>> GetReviewsByBusinessIdAsync(Guid businessId);
    Task<ReviewResponseDto> UpdateReviewAsync(Guid id, UpdateReviewDto dto, Guid? reviewerId, string? email);
}