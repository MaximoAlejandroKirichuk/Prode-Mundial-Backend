namespace Api.Domain.Enums;

public enum RegistrationStatus
{
    Pending = 0,
    Paid = 1,
    Rejected = 2,
    InReview = 3,
    PaidWithoutNotification = 4,
    Notified = 5
}
