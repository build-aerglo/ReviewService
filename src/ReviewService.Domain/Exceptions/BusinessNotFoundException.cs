namespace ReviewService.Domain.Exceptions;

public class BusinessNotFoundException(Guid businessId) : Exception($"Business with ID '{businessId}' was not found.");