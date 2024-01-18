using System.ComponentModel.DataAnnotations;

namespace DebtBot.DB.Entities;

public class Bill
{
    [Key]
    public Guid Id { get; set; }
    public string Currency { get; set; }
    public string Description { get; set; }
    public DateTime Date { get; set; }
}