namespace ReviewService.Application.DTOs;

public record CreateReviewDto(
    Guid BusinessId,
    Guid? LocationId,
    Guid? ReviewerId,       
    string? Email,         
    int StarRating,         
    string ReviewBody,      
    string[]? PhotoUrls,  
    bool ReviewAsAnon
);

public record ReviewResponseDto(
    Guid Id,
    Guid BusinessId,
    Guid? LocationId,
    Guid? ReviewerId,
    string? Email,
    int StarRating,
    string ReviewBody,
    string[]? PhotoUrls,
    bool ReviewAsAnon,
    DateTime CreatedAt
);

public record UpdateReviewDto(
    int? StarRating,
    string? ReviewBody,
    string[]? PhotoUrls
);