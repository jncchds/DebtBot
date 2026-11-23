using DebtBot.Models.User;

namespace DebtBot.Services;

/// <summary>
/// Finds users that look like the same person: the same Telegram id, or the same Telegram username
/// ignoring case and a leading @.
/// </summary>
public static class MergeSuggestions
{
    public static string? NormalizeTelegramUserName(string? userName)
    {
        var trimmed = userName?.Trim().TrimStart('@').Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// Orders users by how good a keeper they are: a Telegram id with the bot enabled first, then a web login or
    /// password, then more bills, then the older one (earliest bill), then the id so the order is stable.
    /// </summary>
    public static IOrderedEnumerable<AdminUserModel> OrderByKeeper(IEnumerable<AdminUserModel> users) =>
        users
            .OrderByDescending(u => u.TelegramId is not null && u.TelegramBotEnabled)
            .ThenByDescending(u => u.Login is not null || u.HasPassword)
            .ThenByDescending(u => u.BillCount)
            .ThenBy(u => u.FirstBillDate is null)
            .ThenBy(u => u.FirstBillDate)
            .ThenBy(u => u.Id);

    public static List<MergeSuggestionModel> Build(IEnumerable<AdminUserModel> users)
    {
        var list = users.ToList();
        var pairs = new Dictionary<(Guid, Guid), MergeSuggestionModel>();

        void addGroups<TKey>(IEnumerable<IGrouping<TKey, AdminUserModel>> groups, MergeReason reason)
        {
            foreach (var group in groups)
            {
                var members = group.ToList();
                for (var i = 0; i < members.Count; i++)
                {
                    for (var j = i + 1; j < members.Count; j++)
                    {
                        var ordered = OrderByKeeper([members[i], members[j]]).ToList();
                        var key = members[i].Id.CompareTo(members[j].Id) < 0
                            ? (members[i].Id, members[j].Id)
                            : (members[j].Id, members[i].Id);

                        if (!pairs.TryGetValue(key, out var suggestion))
                        {
                            suggestion = new MergeSuggestionModel { Keep = ordered[0], Merge = ordered[1] };
                            pairs[key] = suggestion;
                        }
                        if (!suggestion.Reasons.Contains(reason))
                        {
                            suggestion.Reasons.Add(reason);
                        }
                    }
                }
            }
        }

        addGroups(
            list.Where(u => u.TelegramId is not null).GroupBy(u => u.TelegramId!.Value).Where(g => g.Count() > 1),
            MergeReason.SameTelegramId);
        addGroups(
            list.Where(u => NormalizeTelegramUserName(u.TelegramUserName) is not null)
                .GroupBy(u => NormalizeTelegramUserName(u.TelegramUserName)!)
                .Where(g => g.Count() > 1),
            MergeReason.SameTelegramUserName);

        return pairs.Values
            .OrderBy(s => s.Keep.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Merge.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
