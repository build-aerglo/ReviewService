using Microsoft.AspNetCore.Mvc;
using ReviewService.Application.DTOs;
using ReviewService.Domain.Repositories;

namespace ReviewService.Api.Controllers;

/// <summary>
/// Internal API endpoints for Compliance Service to query review data
/// ⚠️ These endpoints should be secured for internal service-to-service communication only
/// </summary>
[ApiController]
[Route("api/internal/review-query")]
public class ReviewQueryController : ControllerBase
{
    private readonly IReviewRepository _reviewRepository;
    private readonly ILogger<ReviewQueryController> _logger;

    public ReviewQueryController(
        IReviewRepository reviewRepository,
        ILogger<ReviewQueryController> logger)
    {
        _reviewRepository = reviewRepository;
        _logger = logger;
    }

    /// <summary>
    /// Check if user has recently reviewed the same business (duplicate check)
    /// </summary>
    [HttpGet("check-duplicate")]
    public async Task<ActionResult<DuplicateCheckResponse>> CheckDuplicate(
        [FromQuery] Guid businessId,
        [FromQuery] Guid? reviewerId = null,
        [FromQuery] string? email = null,
        [FromQuery] string? ipAddress = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] int hours = 72)
    {
        try
        {
            var hasDuplicate = await _reviewRepository.HasRecentReviewAsync(
                businessId,
                reviewerId,
                email,
                ipAddress,
                deviceId,
                TimeSpan.FromHours(hours)
            );

            return Ok(new DuplicateCheckResponse(hasDuplicate));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking duplicate review for business {BusinessId}", businessId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Count how many reviews a user has submitted recently (rate limiting)
    /// </summary>
    [HttpGet("frequency-check")]
    public async Task<ActionResult<FrequencyCheckResponse>> FrequencyCheck(
        [FromQuery] Guid? reviewerId = null,
        [FromQuery] string? email = null,
        [FromQuery] string? ipAddress = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] int hours = 12)
    {
        try
        {
            var count = await _reviewRepository.GetReviewCountAsync(
                reviewerId,
                email,
                ipAddress,
                deviceId,
                TimeSpan.FromHours(hours)
            );

            return Ok(new FrequencyCheckResponse(count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking review frequency");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Check if user has reviewed a business in the same category recently
    /// </summary>
    [HttpGet("category-check")]
    public async Task<ActionResult<CategoryCheckResponse>> CategoryCheck(
        [FromQuery] string category,
        [FromQuery] Guid? reviewerId = null,
        [FromQuery] string? email = null,
        [FromQuery] string? ipAddress = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] int hours = 12)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(category))
                return BadRequest(new { error = "Category is required" });

            var hasReviewed = await _reviewRepository.HasReviewInCategoryAsync(
                reviewerId,
                email,
                ipAddress,
                deviceId,
                category,
                TimeSpan.FromHours(hours)
            );

            return Ok(new CategoryCheckResponse(hasReviewed));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking category review for category {Category}", category);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get review statistics for spike detection
    /// </summary>
    [HttpGet("spike-check")]
    public async Task<ActionResult<SpikeCheckResponse>> SpikeCheck(
        [FromQuery] Guid businessId,
        [FromQuery] int hours = 1)
    {
        try
        {
            var stats = await _reviewRepository.GetReviewStatsAsync(
                businessId,
                TimeSpan.FromHours(hours)
            );

            return Ok(new SpikeCheckResponse(
                stats.TotalReviews,
                stats.PositiveCount,
                stats.NegativeCount,
                stats.ImbalanceRatio
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking spike for business {BusinessId}", businessId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}