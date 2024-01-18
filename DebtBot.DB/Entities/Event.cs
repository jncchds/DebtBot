using DebtBot.DB.Enums;
using System.ComponentModel.DataAnnotations;

namespace DebtBot.DB.Entities;

public class Event
{
    [Key]
    public Guid Id { get; set; }
    
    public DateTime Date { get; set; }
    public string Parameter { get; set; }
    public EventType Type { get; set; }
}