using DebtBot.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebtBot.Models.User;
public class UserSearchModel
{
    public string? DisplayName { get; set; }
    public long? TelegramId { get; set; }
    public string? TelegramUserName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? QueryString { get; set; }
}
