﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebtBot.DB.Entities
{
    public class Bill
    {
        [Key]
        public Guid Id { get; set; }
        public string Currency { get; set; }
        public string Description { get; set; }
        public DateTime Date { get; set; }
    }
}
