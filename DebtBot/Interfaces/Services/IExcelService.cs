using DebtBot.Models.Bill;

namespace DebtBot.Interfaces.Services;
public interface IExcelService
{
    IEnumerable<BillParserModel> Import(Stream file);
}