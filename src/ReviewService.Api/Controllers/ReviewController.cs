using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using ReviewService.Application.DTOs;
using ReviewService.Application.Services;
using ReviewService.Domain.Entities;
using ReviewService.Domain.Exceptions;

namespace ReviewService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _service;
    private readonly DaprClient _daprClient;
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(
        IReviewService service,
        DaprClient daprClient,
        ILogger<ReviewController> logger)
    {
        _service = service;
        _daprClient = daprClient;
        _logger = logger;
    }

    [HttpGet("testing")]
    public IActionResult GetTesting()
    {
        return Ok("Welcome to Review Service");
    }

    [HttpPost]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // ✅ Extract metadata from HTTP context
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var deviceId = Request.Headers["X-Device-Id"].FirstOrDefault();
            var geolocation = Request.Headers["X-Geolocation"].FirstOrDefault();
            var userAgent = Request.Headers["User-Agent"].ToString();

            _logger.LogInformation("Creating review for business {BusinessId} from IP {IpAddress}", 
                dto.BusinessId, ipAddress);

            // Create review with metadata
            var result = await _service.CreateReviewAsync(dto, ipAddress, deviceId, geolocation, userAgent);

            // ✅ Publish message to Kafka via Dapr
            var message = new ReviewSubmittedMessage(
                ReviewId: result.Id,
                BusinessId: result.BusinessId,
                LocationId: result.LocationId,
                ReviewerId: result.ReviewerId,
                Email: result.Email,
                StarRating: result.StarRating,
                ReviewBody: result.ReviewBody,
                PhotoUrls: result.PhotoUrls,
                ReviewAsAnon: result.ReviewAsAnon,
                IpAddress: ipAddress,
                DeviceId: deviceId,
                Geolocation: geolocation,
                UserAgent: userAgent,
                CreatedAt: result.CreatedAt
            );

            await _daprClient.PublishEventAsync(
                pubsubName: "review-pubsub",
                topicName: "review-submitted",
                data: message
            );

            _logger.LogInformation("Published review-submitted event for review {ReviewId}", result.Id);

            // ✅ Return 202 Accepted instead of 201 Created
            return Accepted(new
            {
                reviewId = result.Id,
                status = result.Status,
                message = "Your review is being validated. We'll notify you when it's published!"
            });
        }
        catch (BusinessNotFoundException ex)
        {
            _logger.LogWarning(ex, "Business not found: {BusinessId}", dto.BusinessId);
            return NotFound(new { error = ex.Message });
        }
        catch (UserNotFoundException ex)
        {
            _logger.LogWarning(ex, "User not found: {UserId}", dto.ReviewerId);
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidReviewDataException ex)
        {
            _logger.LogWarning(ex, "Invalid review data: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (ReviewCreationFailedException ex)
        {
            _logger.LogError(ex, "Review creation failed");
            return StatusCode(500, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating review");
            return StatusCode(500, new { error = "Internal server error occurred." });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetReview(Guid id)
    {
        try
        {
            var result = await _service.GetReviewByIdAsync(id);
            return result is not null ? Ok(result) : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review {ReviewId}", id);
            return StatusCode(500, new { error = "Internal server error occurred." });
        }
    }

    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetBusinessReviews(Guid businessId)
    {
        try
        {
            // ✅ Only return APPROVED reviews to public
            var reviews = await _service.GetReviewsByBusinessIdAsync(businessId);
            return Ok(reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews for business {BusinessId}", businessId);
            return StatusCode(500, new { error = "Internal server error occurred." });
        }
    }

    /// <summary>
    /// ✅ NEW: Check review validation status
    /// </summary>
    [HttpGet("status/{reviewId:guid}")]
    public async Task<IActionResult> GetReviewStatus(
        Guid reviewId,
        [FromQuery] string? email = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { error = "Email is required to check review status" });

            var status = await _service.GetReviewStatusAsync(reviewId, email);
            
            if (status == null)
                return NotFound(new { error = "Review not found or email does not match" });

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review status for {ReviewId}", reviewId);
            return StatusCode(500, new { error = "Internal server error occurred." });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateReview(
        Guid id, 
        [FromBody] UpdateReviewDto dto,
        [FromQuery] Guid? reviewerId,
        [FromQuery] string? email)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!reviewerId.HasValue && string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { error = "Either reviewerId or email must be provided for authorization." });
        }

        try
        {
            var result = await _service.UpdateReviewAsync(id, dto, reviewerId, email);
            return Ok(result);
        }
        catch (ReviewNotFoundException ex)
        {
            _logger.LogWarning(ex, "Review not found: {ReviewId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedReviewAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to review: {ReviewId}", id);
            return StatusCode(403, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (ReviewCreationFailedException ex)
        {
            _logger.LogError(ex, "Review update failed");
            return StatusCode(500, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating review {ReviewId}", id);
            return StatusCode(500, new { error = "Internal server error occurred." });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteReview(
        Guid id,
        [FromQuery] Guid? reviewerId,
        [FromQuery] string? email)
    {
        if (!reviewerId.HasValue && string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { error = "Either reviewerId or email must be provided for authorization." });
        }

        try
        {
            await _service.DeleteReviewAsync(id, reviewerId, email);
            return NoContent();
        }
        catch (ReviewNotFoundException ex)
        {
            _logger.LogWarning(ex, "Review not found: {ReviewId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedReviewAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized delete attempt for review: {ReviewId}", id);
            return StatusCode(403, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting review {ReviewId}", id);
            return StatusCode(500, new { error = "Internal server error occurred." });
        }
    }
}