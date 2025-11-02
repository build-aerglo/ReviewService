using ReviewService.Application.DTOs;
using ReviewService.Application.Interfaces;
using ReviewService.Domain.Entities;
using ReviewService.Domain.Exceptions;
using ReviewService.Domain.Repositories;

namespace ReviewService.Application.Services;

public class ReviewService(
    IReviewRepository reviewRepository,
    IBusinessServiceClient businessServiceClient,
    IUserServiceClient userServiceClient
) : IReviewService
{
    public async Task<ReviewResponseDto> CreateReviewAsync(CreateReviewDto dto)
    {
        // ✅ 1. Validate business exists via BusinessService API
        var businessExists = await businessServiceClient.BusinessExistsAsync(dto.BusinessId);
        if (!businessExists)
            throw new BusinessNotFoundException(dto.BusinessId);

        //  Create the review entity
        var review = new Review(
            businessId: dto.BusinessId,
            locationId: dto.LocationId,
            reviewerId: dto.ReviewerId,
            email: dto.Email,
            starRating: dto.StarRating,
            reviewBody: dto.ReviewBody,
            photoUrls: dto.PhotoUrls,
            reviewAsAnon: dto.ReviewAsAnon
        );

        // Save review
        await reviewRepository.AddAsync(review);

        // Confirm save
        var savedReview = await reviewRepository.GetByIdAsync(review.Id);
        if (savedReview is null)
            throw new ReviewCreationFailedException("Failed to create review record.");

        //  Map to response DTO
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
            CreatedAt: savedReview.CreatedAt
        );
    }

    public async Task<ReviewResponseDto?> GetReviewByIdAsync(Guid id)
    {
        var review = await reviewRepository.GetByIdAsync(id);
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
            CreatedAt: review.CreatedAt
        );
    }

    public async Task<IEnumerable<ReviewResponseDto>> GetReviewsByBusinessIdAsync(Guid businessId)
    {
        var reviews = await reviewRepository.GetByBusinessIdAsync(businessId);
        
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
            CreatedAt: r.CreatedAt
        ));
    }

    public async Task<ReviewResponseDto> UpdateReviewAsync(Guid id, UpdateReviewDto dto, Guid? reviewerId, string? email)
    {
        // 1. Get existing review
        var review = await reviewRepository.GetByIdAsync(id);
        if (review is null)
            throw new ReviewNotFoundException(id);

        // 2. Authorization check - user can only edit their own reviews
        var isAuthorized = false;

        if (reviewerId.HasValue && review.ReviewerId.HasValue)
        {
            // Registered user trying to edit their own review
            isAuthorized = review.ReviewerId.Value == reviewerId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(review.Email))
        {
            // Guest user trying to edit their own review
            isAuthorized = review.Email.Equals(email, StringComparison.OrdinalIgnoreCase);
        }

        if (!isAuthorized)
            throw new UnauthorizedReviewAccessException(id);

        // 3. Update the review
        review.Update(dto.StarRating, dto.ReviewBody, dto.PhotoUrls, dto.ReviewAsAnon);

        // 4. Save changes
        await reviewRepository.UpdateAsync(review);

        // 5. Fetch and return updated review
        var updatedReview = await reviewRepository.GetByIdAsync(id);
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
            CreatedAt: updatedReview.CreatedAt
        );
    }
}