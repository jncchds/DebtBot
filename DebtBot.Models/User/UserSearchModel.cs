﻿namespace DebtBot.Models.User;
public class UserSearchModel
{
    public Guid? Id { get; set; }
    public string? DisplayName { get; set; }
    public long? TelegramId { get; set; }
    public string? TelegramUserName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? QueryString { get; set; }
}
