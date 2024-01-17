using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebtBot.DB.Entities
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }
        public string DisplayName { get; set; }
        public long? TelegramId { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }
}
