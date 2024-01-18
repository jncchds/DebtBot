using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DebtBot.DB.Entities;

public class Currency
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [MaxLength(3)]
    public string CurrencyCode { get; set; }
    public string FullName { get; set; }
    public char Character { get; set; }
}