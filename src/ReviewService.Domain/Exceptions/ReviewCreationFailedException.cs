namespace ReviewService.Domain.Exceptions;

public class ReviewCreationFailedException(string message) : Exception(message);