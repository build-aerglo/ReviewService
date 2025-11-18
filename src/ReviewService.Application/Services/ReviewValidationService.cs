using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReviewService.Application.DTOs;
using ReviewService.Application.Interfaces;
using ReviewService.Domain.Entities;
using ReviewService.Domain.Repositories;

namespace ReviewService.Application.Services;

public class ReviewValidationService : IReviewValidationService
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IComplianceServiceClient _complianceClient;
    private readonly INotificationServiceClient _notificationClient;
    private readonly ILogger<ReviewValidationService> _logger;

    public ReviewValidationService(
        IReviewRepository reviewRepository,
        IComplianceServiceClient complianceClient,
        INotificationServiceClient notificationClient,
        ILogger<ReviewValidationService> logger)
    {
        _reviewRepository = reviewRepository;
        _complianceClient = complianceClient;
        _notificationClient = notificationClient;
        _logger = logger;
    }

    public async Task ProcessReviewAsync(ReviewSubmittedMessage message)
    {
        try
        {
            _logger.LogInformation("Processing validation for review {ReviewId}", message.ReviewId);

            // 1. Call Compliance Service
            var validationRequest = new ValidateReviewRequest(
                ReviewId: message.ReviewId,
                BusinessId: message.BusinessId,
                LocationId: message.LocationId,
                ReviewerId: message.ReviewerId,
                Email: message.Email,
                StarRating: message.StarRating,
                ReviewBody: message.ReviewBody,
                IpAddress: message.IpAddress,
                DeviceId: message.DeviceId,
                Geolocation: message.Geolocation,
                UserAgent: message.UserAgent,
                IsGuestUser: !message.ReviewerId.HasValue
            );

            var validationResult = await _complianceClient.ValidateReviewAsync(validationRequest);

            // 2. Retrieve review from database
            var review = await _reviewRepository.GetByIdAsync(message.ReviewId);
            if (review == null)
            {
                _logger.LogError("Review {ReviewId} not found in database", message.ReviewId);
                return;
            }

            // 3. Determine status based on validation result
            string newStatus;
            if (!validationResult.IsValid)
            {
                newStatus = ReviewStatus.Rejected;
            }
            else if (validationResult.Warnings.Any())
            {
                newStatus = ReviewStatus.Flagged;
            }
            else
            {
                newStatus = ReviewStatus.Approved;
            }

            // 4. Update review status in database
            var validationResultJson = JsonSerializer.Serialize(validationResult);
            review.UpdateValidationStatus(newStatus, validationResultJson);
            await _reviewRepository.UpdateAsync(review);

            _logger.LogInformation("Review {ReviewId} status updated to {Status}", 
                message.ReviewId, newStatus);

            // 5. Send notification to user
            var email = message.Email ?? "unknown@example.com"; // Fallback if email is null

            if (newStatus == ReviewStatus.Approved)
            {
                await _notificationClient.SendReviewApprovedAsync(email, message.ReviewId);
            }
            else if (newStatus == ReviewStatus.Rejected)
            {
                await _notificationClient.SendReviewRejectedAsync(
                    email, 
                    message.ReviewId, 
                    validationResult.Errors);
            }
            else if (newStatus == ReviewStatus.Flagged)
            {
                await _notificationClient.SendReviewFlaggedAsync(email, message.ReviewId);
            }

            _logger.LogInformation("Validation processing completed for review {ReviewId}", message.ReviewId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing validation for review {ReviewId}", message.ReviewId);
            // Dapr will retry based on resiliency configuration
            throw;
        }
    }
}