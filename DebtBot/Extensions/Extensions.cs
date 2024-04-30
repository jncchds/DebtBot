using AutoMapper;
using DebtBot.Messages;
using DebtBot.Models;
using DebtBot.Telegram.Commands.CallbackQueries;
using System.Text;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Extensions
{
    public static class Extensions
    {
        public static async Task<string> ReadToEndAsync(this Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync();
            }
        }
        
        public static (int index, string val) WhitespaceLocator(string w)
            => (index: w.IndexOfAny([' ', '\n', '\t', '\v', '\r']), val: w);

        public static PagingResult<T> ToPagingResult<T>(this IQueryable<T> query, int pageNumber = 0, int? countPerPage = null)
        {
            var count = query.Count();
            var items = query.Skip(pageNumber * countPerPage ?? 0).Take(countPerPage ?? count).ToList();
            return new PagingResult<T>(countPerPage ?? count, pageNumber, count, items);
        }

        public static List<InlineButtonRecord> ToInlineKeyboardButtons<T>(this PagingResult<T> pagingResults, string commandName)
        {
            var buttons = new List<InlineButtonRecord>
            {
                !pagingResults.MoreThenFivePassed
                ? new(" ", IgnoreCallbackQuery.CommandString)
                : new("<<", $"{commandName} {pagingResults.PageNumber - 5}"),

                pagingResults.IsFirstPage
                ? new(" ", IgnoreCallbackQuery.CommandString)
                : new("<", $"{commandName} {pagingResults.PageNumber - 1}"),

                new($"{pagingResults.PageNumber + 1}/{pagingResults.LastPageNumber + 1}", IgnoreCallbackQuery.CommandString),

                pagingResults.IsLastPage
                ? new(" ", IgnoreCallbackQuery.CommandString)
                : new(">", $"{commandName} {pagingResults.PageNumber + 1}"),

                !pagingResults.MoreThenFiveLeft
                ? new(" ", IgnoreCallbackQuery.CommandString)
                : new(">>", $"{commandName} {pagingResults.PageNumber + 5}")
            };

            return buttons;
        }
    }
}
