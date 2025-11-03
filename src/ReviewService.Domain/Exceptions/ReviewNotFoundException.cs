namespace ReviewService.Domain.Exceptions;

public class ReviewNotFoundException(Guid reviewId) : Exception($"Review with ID '{reviewId}' was not found.");