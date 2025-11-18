using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ReviewService.Application.Interfaces;

namespace ReviewService.Infrastructure.Clients;

public class NotificationServiceClient : INotificationServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationServiceClient> _logger;

    public NotificationServiceClient(HttpClient httpClient, ILogger<NotificationServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task SendReviewApprovedAsync(string email, Guid reviewId)
    {
        try
        {
            _logger.LogInformation("Sending review approved notification to {Email} for review {ReviewId}", 
                email, reviewId);

            var payload = new
            {
                Email = email,
                ReviewId = reviewId
            };

            var response = await _httpClient.PostAsJsonAsync("/api/notification/review-approved", payload);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to send approved notification: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Don't throw - notification failure shouldn't fail the validation
            _logger.LogError(ex, "Error sending review approved notification for review {ReviewId}", reviewId);
        }
    }

    public async Task SendReviewRejectedAsync(string email, Guid reviewId, List<string> reasons)
    {
        try
        {
            _logger.LogInformation("Sending review rejected notification to {Email} for review {ReviewId}", 
                email, reviewId);

            var payload = new
            {
                Email = email,
                ReviewId = reviewId,
                Reasons = reasons
            };

            var response = await _httpClient.PostAsJsonAsync("/api/notification/review-rejected", payload);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to send rejected notification: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending review rejected notification for review {ReviewId}", reviewId);
        }
    }

    public async Task SendReviewFlaggedAsync(string email, Guid reviewId)
    {
        try
        {
            _logger.LogInformation("Sending review flagged notification to {Email} for review {ReviewId}", 
                email, reviewId);

            var payload = new
            {
                Email = email,
                ReviewId = reviewId
            };

            var response = await _httpClient.PostAsJsonAsync("/api/notification/review-flagged", payload);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to send flagged notification: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending review flagged notification for review {ReviewId}", reviewId);
        }
    }
}