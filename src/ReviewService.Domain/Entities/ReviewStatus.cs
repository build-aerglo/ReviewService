namespace ReviewService.Domain.Entities;

public static class ReviewStatus
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Flagged = "FLAGGED";

    public static bool IsValid(string status)
    {
        return status == Pending || 
               status == Approved || 
               status == Rejected || 
               status == Flagged;
    }
}