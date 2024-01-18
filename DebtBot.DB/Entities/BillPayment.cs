﻿using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

public class BillPayment
{
    public Guid BillId { get; set; }
    public Guid UserId { get; set; }
    
    [Column(TypeName = "decimal(10, 4)")]
    public decimal Amount { get; set; }

    [ForeignKey(nameof(BillId))]
    virtual public Bill Bill { get; set; }
    [ForeignKey(nameof(UserId))]
    virtual public User User { get; set; }
}