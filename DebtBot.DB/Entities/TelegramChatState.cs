using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

/// <summary>What the bot remembers about a Telegram chat (private or group).</summary>
public class TelegramChatState
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long ChatId { get; set; }

    /// <summary>When the receipt privacy note was first sent here; afterwards replies carry only a short footer.</summary>
    public DateTime? ReceiptNoteShownAt { get; set; }
}
