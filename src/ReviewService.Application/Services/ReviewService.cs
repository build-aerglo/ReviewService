using System.Text.Json;
using ReviewService.Application.DTOs;
using ReviewService.Application.Interfaces;
using ReviewService.Domain.Entities;
using ReviewService.Domain.Exceptions;
using ReviewService.Domain.Repositories;

namespace ReviewService.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IBusinessServiceClient _businessServiceClient;
    private readonly IUserServiceClient _userServiceClient;

    public ReviewService(
        IReviewRepository reviewRepository,
        IBusinessServiceClient businessServiceClient,
        IUserServiceClient userServiceClient)
    {
        _reviewRepository = reviewRepository;
        _businessServiceClient = businessServiceClient;
        _userServiceClient = userServiceClient;
    }

    public async Task<ReviewResponseDto> CreateReviewAsync(
        CreateReviewDto dto,
        string? ipAddress = null,
        string? deviceId = null,
        string? geolocation = null,
        string? userAgent = null)
    {
        // ✅ 1. Validate business exists via BusinessService API
        var businessExists = await _businessServiceClient.BusinessExistsAsync(dto.BusinessId);
        if (!businessExists)
            throw new BusinessNotFoundException(dto.BusinessId);

        // ✅ 2. Create the review entity with metadata
        var review = new Review(
            businessId: dto.BusinessId,
            locationId: dto.LocationId,
            reviewerId: dto.ReviewerId,
            email: dto.Email,
            starRating: dto.StarRating,
            reviewBody: dto.ReviewBody,
            photoUrls: dto.PhotoUrls,
            reviewAsAnon: dto.ReviewAsAnon,
            ipAddress: ipAddress,
            deviceId: deviceId,
            geolocation: geolocation,
            userAgent: userAgent
        );

        // ✅ 3. Save review with PENDING status
        await _reviewRepository.AddAsync(review);

        // ✅ 4. Confirm save
        var savedReview = await _reviewRepository.GetByIdAsync(review.Id);
        if (savedReview is null)
            throw new ReviewCreationFailedException("Failed to create review record.");

        // ✅ 5. Map to response DTO
        return new ReviewResponseDto(
            Id: savedReview.Id,
            BusinessId: savedReview.BusinessId,
            LocationId: savedReview.LocationId,
            ReviewerId: savedReview.ReviewerId,
            Email: savedReview.Email,
            StarRating: savedReview.StarRating,
            ReviewBody: savedReview.ReviewBody,
            PhotoUrls: savedReview.PhotoUrls,
            ReviewAsAnon: savedReview.ReviewAsAnon,
            CreatedAt: savedReview.CreatedAt,
            Status: savedReview.Status,
            ValidatedAt: savedReview.ValidatedAt
        );
    }

    public async Task<ReviewResponseDto?> GetReviewByIdAsync(Guid id)
    {
        var review = await _reviewRepository.GetByIdAsync(id);
        if (review is null)
            return null;

        return new ReviewResponseDto(
            Id: review.Id,
            BusinessId: review.BusinessId,
            LocationId: review.LocationId,
            ReviewerId: review.ReviewerId,
            Email: review.Email,
            StarRating: review.StarRating,
            ReviewBody: review.ReviewBody,
            PhotoUrls: review.PhotoUrls,
            ReviewAsAnon: review.ReviewAsAnon,
            CreatedAt: review.CreatedAt,
            Status: review.Status,
            ValidatedAt: review.ValidatedAt
        );
    }

    public async Task<IEnumerable<ReviewResponseDto>> GetReviewsByBusinessIdAsync(Guid businessId)
    {
        // ✅ Only return APPROVED reviews to public
        var reviews = await _reviewRepository.GetByBusinessIdAsync(businessId, ReviewStatus.Approved);
        
        return reviews.Select(r => new ReviewResponseDto(
            Id: r.Id,
            BusinessId: r.BusinessId,
            LocationId: r.LocationId,
            ReviewerId: r.ReviewerId,
            Email: r.Email,
            StarRating: r.StarRating,
            ReviewBody: r.ReviewBody,
            PhotoUrls: r.PhotoUrls,
            ReviewAsAnon: r.ReviewAsAnon,
            CreatedAt: r.CreatedAt,
            Status: r.Status,
            ValidatedAt: r.ValidatedAt
        ));
    }

    /// <summary>
    /// ✅ NEW: Get review status for user to check validation progress
    /// </summary>
    public async Task<ReviewStatusDto?> GetReviewStatusAsync(Guid reviewId, string email)
    {
        var review = await _reviewRepository.GetByIdAsync(reviewId);
        
        if (review == null)
            return null;

        // Verify email matches (security check)
        if (!string.Equals(review.Email, email, StringComparison.OrdinalIgnoreCase))
            return null;

        ValidationResult? validationResult = null;
        if (!string.IsNullOrWhiteSpace(review.ValidationResult))
        {
            try
            {
                validationResult = JsonSerializer.Deserialize<ValidationResult>(review.ValidationResult);
            }
            catch
            {
                // If deserialization fails, just return null
            }
        }

        return new ReviewStatusDto(
            ReviewId: review.Id,
            Status: review.Status,
            ValidatedAt: review.ValidatedAt,
            ValidationResult: validationResult
        );
    }

    public async Task<ReviewResponseDto> UpdateReviewAsync(Guid id, UpdateReviewDto dto, Guid? reviewerId, string? email)
    {
        // 1. Get existing review
        var review = await _reviewRepository.GetByIdAsync(id);
        if (review is null)
            throw new ReviewNotFoundException(id);

        // 2. Authorization check - user can only edit their own reviews
        var isAuthorized = false;

        if (reviewerId.HasValue && review.ReviewerId.HasValue)
        {
            isAuthorized = review.ReviewerId.Value == reviewerId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(review.Email))
        {
            isAuthorized = review.Email.Equals(email, StringComparison.OrdinalIgnoreCase);
        }

        if (!isAuthorized)
            throw new UnauthorizedReviewAccessException(id);

        // 3. Update the review
        review.Update(dto.StarRating, dto.ReviewBody, dto.PhotoUrls, dto.ReviewAsAnon);

        // 4. Save changes
        await _reviewRepository.UpdateAsync(review);

        // 5. Fetch and return updated review
        var updatedReview = await _reviewRepository.GetByIdAsync(id);
        if (updatedReview is null)
            throw new ReviewCreationFailedException("Failed to update review record.");

        return new ReviewResponseDto(
            Id: updatedReview.Id,
            BusinessId: updatedReview.BusinessId,
            LocationId: updatedReview.LocationId,
            ReviewerId: updatedReview.ReviewerId,
            Email: updatedReview.Email,
            StarRating: updatedReview.StarRating,
            ReviewBody: updatedReview.ReviewBody,
            PhotoUrls: updatedReview.PhotoUrls,
            ReviewAsAnon: updatedReview.ReviewAsAnon,
            CreatedAt: updatedReview.CreatedAt,
            Status: updatedReview.Status,
            ValidatedAt: updatedReview.ValidatedAt
        );
    }
    
    public async Task DeleteReviewAsync(Guid id, Guid? reviewerId, string? email)
    {
        // 1. Get existing review
        var review = await _reviewRepository.GetByIdAsync(id);
        if (review is null)
            throw new ReviewNotFoundException(id);

        // 2. Authorization check - user can only delete their own reviews
        var isAuthorized = false;

        if (reviewerId.HasValue && review.ReviewerId.HasValue)
        {
            isAuthorized = review.ReviewerId.Value == reviewerId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(review.Email))
        {
            isAuthorized = review.Email.Equals(email, StringComparison.OrdinalIgnoreCase);
        }

        if (!isAuthorized)
            throw new UnauthorizedReviewAccessException(id);

        // 3. Delete the review
        await _reviewRepository.DeleteAsync(id);
    }
}