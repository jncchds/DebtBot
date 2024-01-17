using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebtBot.DB.Entities
{
    public class BillLineParticipant
    {
        public long BillLineId { get; set; }
        public Guid UserId { get; set; }

        [Column(TypeName = "decimal(10, 4)")]
        public decimal Part { get; set; }

        [ForeignKey(nameof(BillLineId))]
        public virtual BillLine BillLine { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; }
    }
}
