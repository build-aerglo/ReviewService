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
    DateTime CreatedAt,
    string Status,
    DateTime? ValidatedAt
);

public record UpdateReviewDto(
    int? StarRating,
    string? ReviewBody,
    string[]? PhotoUrls,
    bool? ReviewAsAnon
);

//  Message published to Kafka
public record ReviewSubmittedMessage(
    Guid ReviewId,
    Guid BusinessId,
    Guid? LocationId,
    Guid? ReviewerId,
    string? Email,
    int StarRating,
    string ReviewBody,
    string[]? PhotoUrls,
    bool ReviewAsAnon,
    string? IpAddress,
    string? DeviceId,
    string? Geolocation,
    string? UserAgent,
    DateTime CreatedAt
);

//  Request to Compliance Service
public record ValidateReviewRequest(
    Guid ReviewId,
    Guid BusinessId,
    Guid? LocationId,
    Guid? ReviewerId,
    string? Email,
    int StarRating,
    string ReviewBody,
    string? IpAddress,
    string? DeviceId,
    string? Geolocation,
    string? UserAgent,
    bool IsGuestUser
);

//  Response from Compliance Service
public record ValidationResult(
    bool IsValid,
    int Level,
    List<string> Errors,
    List<string> Warnings,
    List<string> ExecutedRules,
    DateTime Timestamp
);

//  DTOs for internal query endpoints
public record DuplicateCheckResponse(bool HasDuplicate);
public record FrequencyCheckResponse(int Count);
public record CategoryCheckResponse(bool HasReviewed);
public record SpikeCheckResponse(
    int TotalReviews,
    int PositiveReviews,
    int NegativeReviews,
    double ImbalanceRatio
);

//  Review status check DTO
public record ReviewStatusDto(
    Guid ReviewId,
    string Status,
    DateTime? ValidatedAt,
    ValidationResult? ValidationResult
);



