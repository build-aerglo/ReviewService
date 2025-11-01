namespace ReviewService.Application.Interfaces;

public interface ILocationServiceClient
{
    Task<bool> LocationExistsAsync(Guid locationId);
}