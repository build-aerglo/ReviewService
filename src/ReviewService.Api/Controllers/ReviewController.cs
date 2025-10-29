


using Microsoft.AspNetCore.Mvc;

namespace ReviewService.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class ReviewController(ILogger<ReviewController> logger) : ControllerBase
{
    [HttpGet("testing")]
    public async Task<IActionResult> GetTesting()
    {
        return Ok("Welcome to Review Service");
    }
}