namespace ReviewService.Application.Interfaces;

public interface IBusinessServiceClient
{
    Task<bool> BusinessExistsAsync(Guid businessId);
}

