namespace ReviewService.Domain.Entities;

public class Review
{
    public Guid Id { get; private set; }
    public Guid BusinessId { get; private set; }
    public Guid? LocationId { get; private set; }
    public Guid? ReviewerId { get; private set; }
    public string? Email { get; private set; }
    public int StarRating { get; private set; }
    public string ReviewBody { get; private set; } = default!;
    public string[]? PhotoUrls { get; private set; }
    public bool ReviewAsAnon { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // 🟢 Parameterless constructor for Dapper
    protected Review() { }

    // ✅ Domain-level constructor (for creating new reviews in code)
    public Review(
        Guid businessId,
        Guid? locationId,
        Guid? reviewerId,
        string? email,
        int starRating,
        string reviewBody,
        string[]? photoUrls,
        bool reviewAsAnon)
    {
        // Validations
        if (starRating < 1 || starRating > 5)
            throw new ArgumentException("Star rating must be between 1 and 5.", nameof(starRating));

        if (string.IsNullOrWhiteSpace(reviewBody) || reviewBody.Length < 20 || reviewBody.Length > 500)
            throw new ArgumentException("Review body must be between 20 and 500 characters.", nameof(reviewBody));

        if (photoUrls is not null && photoUrls.Length > 3)
            throw new ArgumentException("Maximum 3 photos allowed.", nameof(photoUrls));

        // If reviewer is guest (no reviewerId), email is required
        if (!reviewerId.HasValue && string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required for guest reviews.", nameof(email));

        Id = Guid.NewGuid();
        BusinessId = businessId;
        LocationId = locationId;
        ReviewerId = reviewerId;
        Email = email;
        StarRating = starRating;
        ReviewBody = reviewBody;
        PhotoUrls = photoUrls;
        ReviewAsAnon = reviewAsAnon;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(int? starRating, string? reviewBody, string[]? photoUrls)
    {
        if (starRating.HasValue)
        {
            if (starRating.Value < 1 || starRating.Value > 5)
                throw new ArgumentException("Star rating must be between 1 and 5.", nameof(starRating));
            StarRating = starRating.Value;
        }

        if (!string.IsNullOrWhiteSpace(reviewBody))
        {
            if (reviewBody.Length < 20 || reviewBody.Length > 500)
                throw new ArgumentException("Review body must be between 20 and 500 characters.", nameof(reviewBody));
            ReviewBody = reviewBody;
        }

        if (photoUrls is not null)
        {
            if (photoUrls.Length > 3)
                throw new ArgumentException("Maximum 3 photos allowed.", nameof(photoUrls));
            PhotoUrls = photoUrls;
        }

        UpdatedAt = DateTime.UtcNow;
    }
}