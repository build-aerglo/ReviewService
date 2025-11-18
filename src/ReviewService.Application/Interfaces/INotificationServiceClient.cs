namespace ReviewService.Application.Interfaces;

public interface INotificationServiceClient
{
    Task SendReviewApprovedAsync(string email, Guid reviewId);
    Task SendReviewRejectedAsync(string email, Guid reviewId, List<string> reasons);
    Task SendReviewFlaggedAsync(string email, Guid reviewId);
}