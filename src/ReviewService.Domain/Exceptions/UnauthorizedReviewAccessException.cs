namespace ReviewService.Domain.Exceptions;

public class UnauthorizedReviewAccessException (Guid reviewId) : Exception($"Unauthorized access to review with ID '{reviewId}'. You can only edit your own reviews.");