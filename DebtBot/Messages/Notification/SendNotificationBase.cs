using System.Text.Json.Serialization;

namespace DebtBot.Messages.Notification;

[JsonPolymorphic(TypeDiscriminatorPropertyName = nameof(NotificationType))]
[JsonDerivedType(typeof(SendBillProcessedNotification), (int)NotificationType.BillProcessed )]
[JsonDerivedType(typeof(SendBillNotification), (int)NotificationType.Bill )]
public class SendNotificationBase
{
    public virtual NotificationType NotificationType { get; }
}
