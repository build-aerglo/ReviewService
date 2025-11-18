using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ReviewService.Application.DTOs;
using ReviewService.Application.Interfaces;

namespace ReviewService.Infrastructure.Clients;

public class ComplianceServiceClient : IComplianceServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ComplianceServiceClient> _logger;

    public ComplianceServiceClient(HttpClient httpClient, ILogger<ComplianceServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateReviewAsync(ValidateReviewRequest request)
    {
        try
        {
            _logger.LogInformation("Calling Compliance Service to validate review {ReviewId}", request.ReviewId);

            var response = await _httpClient.PostAsJsonAsync("/api/compliance/validate-review", request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Compliance Service returned error: {StatusCode}", response.StatusCode);
                
                // Return failed validation result instead of throwing
                return new ValidationResult(
                    IsValid: false,
                    Level: 0,
                    Errors: new List<string> { "Validation service unavailable" },
                    Warnings: new List<string>(),
                    ExecutedRules: new List<string>(),
                    Timestamp: DateTime.UtcNow
                );
            }

            var result = await response.Content.ReadFromJsonAsync<ValidationResult>();
            
            if (result == null)
            {
                _logger.LogError("Failed to deserialize validation result from Compliance Service");
                return new ValidationResult(
                    IsValid: false,
                    Level: 0,
                    Errors: new List<string> { "Invalid validation response" },
                    Warnings: new List<string>(),
                    ExecutedRules: new List<string>(),
                    Timestamp: DateTime.UtcNow
                );
            }

            _logger.LogInformation("Review {ReviewId} validation completed: Valid={IsValid}, Level={Level}", 
                request.ReviewId, result.IsValid, result.Level);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error calling Compliance Service for review {ReviewId}", request.ReviewId);
            
            return new ValidationResult(
                IsValid: false,
                Level: 0,
                Errors: new List<string> { "Network error contacting validation service" },
                Warnings: new List<string>(),
                ExecutedRules: new List<string>(),
                Timestamp: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating review {ReviewId}", request.ReviewId);
            
            return new ValidationResult(
                IsValid: false,
                Level: 0,
                Errors: new List<string> { "Unexpected validation error" },
                Warnings: new List<string>(),
                ExecutedRules: new List<string>(),
                Timestamp: DateTime.UtcNow
            );
        }
    }
}