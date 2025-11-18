using System.Net;
using Microsoft.Extensions.Logging;
using ReviewService.Application.Interfaces;

namespace ReviewService.Infrastructure.Clients;

public class BusinessServiceClient(HttpClient httpClient, ILogger<BusinessServiceClient> logger)
    : IBusinessServiceClient
{
    public async Task<bool> BusinessExistsAsync(Guid businessId)
    {
        try
        {
            var response = await httpClient.GetAsync($"/api/Business/{businessId}");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                logger.LogInformation(" Business {BusinessId} exists.", businessId);
                return true;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogWarning(" Business {BusinessId} not found.", businessId);
                return false;
            }

            logger.LogWarning("Unexpected response from Business Service: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error checking if business {BusinessId} exists.", businessId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error checking business existence for {BusinessId}.", businessId);
            return false;
        }
    }
}