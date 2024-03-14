using DebtBot.Models.Bill;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;
public interface IExcelService
{
    List<BillParserModel> Import(Stream file, Guid creator, Dictionary<int, Guid> users);
    Dictionary<int, UserSearchModel> ImportUsers(Stream file);
}