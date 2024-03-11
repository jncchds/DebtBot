using DebtBot.Models.Bill;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;
public interface IExcelService
{
    List<BillImportModel> Import(Stream file, Guid creator, Dictionary<int, UserModel> users);
    Dictionary<int, UserSearchModel> ImportUsers(Stream file);
}