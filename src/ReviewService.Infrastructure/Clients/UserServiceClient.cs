using System.Net;
using Microsoft.Extensions.Logging;
using ReviewService.Application.Interfaces;

namespace ReviewService.Infrastructure.Clients;

public class UserServiceClient(HttpClient httpClient, ILogger<UserServiceClient> logger)
    : IUserServiceClient
{
    public async Task<bool> UserExistsAsync(Guid userId)
    {
        try
        {
            var response = await httpClient.GetAsync($"/api/users/{userId}");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                logger.LogInformation(" User {UserId} exists.", userId);
                return true;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogWarning(" User {UserId} not found.", userId);
                return false;
            }

            logger.LogWarning(" Unexpected response from User Service: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error checking if user {UserId} exists.", userId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error checking user existence for {UserId}.", userId);
            return false;
        }
    }
}