using ReviewService.Application.DTOs;

namespace ReviewService.Application.Services;

public interface IReviewValidationService
{
    Task ProcessReviewAsync(ReviewSubmittedMessage message);
}