namespace ReviewService.Domain.Exceptions;

public class UserNotFoundException(Guid userId) : Exception($"User with ID '{userId}' was not found.");