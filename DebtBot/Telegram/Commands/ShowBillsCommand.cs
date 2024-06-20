using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Services;
using MassTransit;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Telegram.Commands;

public class ShowBillsCommand: BillsCommand, ITelegramCommand, ITelegramCallbackQuery
{
	public const string CommandString = "/ShowBills";
	public string CommandName => CommandString;

    public ShowBillsCommand(
		IBillService billService,
        IUserService userService,
        ITelegramService telegramService,
		IOptions<DebtBotConfiguration> debtBotConfig,
		IPublishEndpoint publishEndpoint)
		: base(billService, userService, telegramService, debtBotConfig, publishEndpoint)
	{
    }
}
