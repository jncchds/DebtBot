using System.Text.Json.Serialization;

namespace DebtBot.Messages.Notification;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "NotificationType")]
[JsonDerivedType(typeof(SendBillProcessedNotificationMessage), (int)NotificationType.BillProcessed )]
[JsonDerivedType(typeof(SendShowBillNotificationMessage), (int)NotificationType.ShowBill )]
public class SendNotificationBaseMessage
{
    public virtual NotificationType NotificationType { get; }
}
