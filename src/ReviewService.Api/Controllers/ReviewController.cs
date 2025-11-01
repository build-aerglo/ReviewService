using Microsoft.AspNetCore.Mvc;
using ReviewService.Application.DTOs;
using ReviewService.Application.Services;
using ReviewService.Domain.Exceptions;

namespace ReviewService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController(IReviewService service, ILogger<ReviewController> logger) : ControllerBase
{
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
            var result = await service.CreateReviewAsync(dto);

            var location = Url.Action("GetReview", "Review", new { id = result.Id });
            return Created(location ?? string.Empty, result);
        }
        catch (BusinessNotFoundException ex)
        {
            logger.LogWarning(ex, "Business not found: {BusinessId}", dto.BusinessId);
            return NotFound(new { error = ex.Message });
        }
        catch (UserNotFoundException ex)
        {
            logger.LogWarning(ex, "User not found: {UserId}", dto.ReviewerId);
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidReviewDataException ex)
        {
            logger.LogWarning(ex, "Invalid review data: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (ReviewCreationFailedException ex)
        {
            logger.LogError(ex, "Review creation failed");
            return StatusCode(500, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Validation error: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error creating review");
            return StatusCode(500, new { error = "Internal server error occurred." });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetReview(Guid id)
    {
        try
        {
            var result = await service.GetReviewByIdAsync(id);
            return result is not null ? Ok(result) : NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving review {ReviewId}", id);
            return StatusCode(500, new { error = "Internal server error occurred." });
        }
    }

    [HttpGet("business/{businessId:guid}")]
    public async Task<IActionResult> GetBusinessReviews(Guid businessId)
    {
        try
        {
            var reviews = await service.GetReviewsByBusinessIdAsync(businessId);
            return Ok(reviews);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving reviews for business {BusinessId}", businessId);
            return StatusCode(500, new { error = "Internal server error occurred." });
        }
    }
}