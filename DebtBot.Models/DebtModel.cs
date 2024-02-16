using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DebtBot.Models;

public class DebtModel
{
    public Guid CreditorUserId { get; set; }
    public string CreditorDisplayName { get; set; }
    public Guid DebtorUserId { get; set; }
    public string DebtorDisplayName { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; }
}
