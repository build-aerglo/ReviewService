using ReviewService.Application.DTOs;

namespace ReviewService.Application.Interfaces;

public interface IComplianceServiceClient
{
    Task<ValidationResult> ValidateReviewAsync(ValidateReviewRequest request);
}