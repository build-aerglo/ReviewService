namespace ReviewService.Domain.Exceptions;

public class InvalidReviewDataException(string message) : Exception(message);