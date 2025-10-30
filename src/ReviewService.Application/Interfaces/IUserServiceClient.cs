namespace ReviewService.Application.Interfaces;

public interface IUserServiceClient
{
    Task<bool> UserExistsAsync(Guid userId);
}