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
            // The page and the count are two queries, and data can change between them (a bill finalized in the
            // background, a new one created). Read the page first and keep the count consistent with it:
            // - no page size: one query, the count is the list;
            // - a short page is the last page, so the count follows from it;
            // - a full page: count separately, but never below what this page already shows.
            if (countPerPage is null)
            {
                var all = query.ToList();
                return new PagingResult<T>(all.Count, pageNumber, all.Count, all);
            }

            var size = countPerPage.Value;
            var skip = pageNumber * size;
            var items = query.Skip(skip).Take(size).ToList();
            var count = items.Count < size && (items.Count > 0 || skip == 0)
                ? skip + items.Count
                : items.Count == 0 ? query.Count() : Math.Max(query.Count(), skip + items.Count);
            return new PagingResult<T>(size, pageNumber, count, items);
        }

        public static List<InlineButtonRecord> ToInlineKeyboardButtons<T>(this PagingResult<T> pagingResults, string commandName, string parameters = "")
        {
            var buttons = new List<InlineButtonRecord>
            {
                !pagingResults.MoreThenFivePassed
                ? new(" ", IgnoreCallbackQuery.CommandString)
                : new("<<", $"{commandName} {pagingResults.PageNumber - 5} {parameters}"),

                pagingResults.IsFirstPage
                ? new(" ", IgnoreCallbackQuery.CommandString)
                : new("<", $"{commandName} {pagingResults.PageNumber - 1} {parameters}"),

                new($"{pagingResults.PageNumber + 1}/{pagingResults.LastPageNumber + 1}", IgnoreCallbackQuery.CommandString),

                pagingResults.IsLastPage
                ? new(" ", IgnoreCallbackQuery.CommandString)
                : new(">", $"{commandName} {pagingResults.PageNumber + 1} {parameters}"),

                !pagingResults.MoreThenFiveLeft
                ? new(" ", IgnoreCallbackQuery.CommandString)
                : new(">>", $"{commandName} {pagingResults.PageNumber + 5} {parameters}")
            };

            return buttons;
        }
    }
}
