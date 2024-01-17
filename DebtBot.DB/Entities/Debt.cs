﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebtBot.DB.Entities
{
    public class Debt
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey(nameof(CreditorUser))]
        public Guid CreditorUserId { get; set; }

        [ForeignKey(nameof(DebtorUser))]
        public Guid DebtorUserId { get; set; }

        [Column(TypeName = "decimal(10, 4)")]
        public decimal Amount { get; set; }

        [MaxLength(3)]
        public string Currency { get; set; }

        [ForeignKey(nameof(CreditorUserId))]
        public virtual User CreditorUser { get; set; }

        [ForeignKey(nameof(DebtorUserId))]
        public virtual User DebtorUser { get; set; }
    }
}