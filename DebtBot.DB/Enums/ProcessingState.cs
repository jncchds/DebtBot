using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebtBot.DB.Enums
{
    public enum ProcessingState : byte
    {
        Draft = 0,
        Ready = 1,
        Processing = 2,
        Processed = 3
    }
}
