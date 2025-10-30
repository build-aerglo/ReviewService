using System.Net;
using Microsoft.Extensions.Logging;
using ReviewService.Application.Interfaces;

namespace ReviewService.Infrastructure.Clients;

public class LocationServiceClient(HttpClient httpClient, ILogger<LocationServiceClient> logger)
    : ILocationServiceClient
{
    public async Task<bool> LocationExistsAsync(Guid locationId)
    {
        try
        {
            var response = await httpClient.GetAsync($"/api/locations/{locationId}");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                logger.LogInformation("✅ Location {LocationId} exists.", locationId);
                return true;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogWarning("❌ Location {LocationId} not found.", locationId);
                return false;
            }

            logger.LogWarning("⚠️ Unexpected response from Location Service: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error checking if location {LocationId} exists.", locationId);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error checking location existence for {LocationId}.", locationId);
            return false;
        }
    }
}