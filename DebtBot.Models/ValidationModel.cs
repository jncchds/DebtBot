using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebtBot.Models;
public class ValidationModel<T>
{
    public T? Result { get; set; }
    public List<string> Errors { get; set; } = new List<string>();

    public bool IsValid => !Errors.Any();
}
