using Dapr;
using Microsoft.AspNetCore.Mvc;
using ReviewService.Application.DTOs;
using ReviewService.Application.Services;

namespace ReviewService.Api.Controllers;

/// <summary>
/// Handles events from Dapr pub/sub (Kafka messages)
/// This is the background worker that processes reviews asynchronously
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ReviewEventController : ControllerBase
{
    private readonly IReviewValidationService _validationService;
    private readonly ILogger<ReviewEventController> _logger;

    public ReviewEventController(
        IReviewValidationService validationService,
        ILogger<ReviewEventController> logger)
    {
        _validationService = validationService;
        _logger = logger;
    }

    /// <summary>
    /// Dapr subscription endpoint for review-submitted topic
    /// This method is called automatically by Dapr when a message is published to Kafka
    /// </summary>
    [Topic("review-pubsub", "review-submitted")]
    [HttpPost("review-submitted")]
    public async Task<IActionResult> HandleReviewSubmitted([FromBody] ReviewSubmittedMessage message)
    {
        try
        {
            _logger.LogInformation("Received review-submitted event for review {ReviewId}", message.ReviewId);

            await _validationService.ProcessReviewAsync(message);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling review-submitted event for review {ReviewId}", message.ReviewId);
            
            // Return 500 to trigger Dapr retry
            return StatusCode(500, new { error = "Failed to process review validation" });
        }
    }
}