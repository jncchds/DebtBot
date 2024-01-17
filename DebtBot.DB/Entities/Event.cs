using DebtBot.DB.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebtBot.DB.Entities
{
    public class Event
    {
        [Key]
        public long Id { get; set; }
        public DateTime Date { get; set; }
        public string Parameter { get; set; }
        public EventType Type { get; set; }
    }
}
