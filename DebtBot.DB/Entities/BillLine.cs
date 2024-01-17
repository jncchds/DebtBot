using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebtBot.DB.Entities
{
    public class BillLine
    {
        [Key]
        public Guid Id { get; set; }

        public long BillId { get; set; }

        public string ItemDescription { get; set; }
        public decimal Subtotal { get; set; }
        [ForeignKey(nameof(BillId))]
        public virtual Bill Bill { get; set; }
    }
}
