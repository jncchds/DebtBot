﻿using DebtBot.Interfaces.Services;
using DebtBot.Models.Bill;
using DebtBot.Models.User;
using OfficeOpenXml;
using System.Data.SqlTypes;
using System.Globalization;

namespace DebtBot.Services;

public class ExcelService : IExcelService
{
    public Dictionary<int, UserSearchModel> ImportUsers(Stream file)
    {
        using var package = new ExcelPackage(file);

        var worksheet = package.Workbook.Worksheets[0];

        int colCount = worksheet.Dimension.Columns;

        Dictionary<int, UserSearchModel> users = [];

        for(int col = 9; col <= colCount; col++)
        {
            if (String.IsNullOrEmpty(worksheet.Cells[1, col].Value?.ToString()))
                continue;

            users.Add(col, new UserSearchModel()
            {
                QueryString = worksheet.Cells[1, col].Value.ToString()!
            });
        }

        return users;
    }

    public List<BillImportModel> Import(Stream file, Guid creator, Dictionary<int, UserModel> users)
    {

        using var package = new ExcelPackage(file);

        var worksheet = package.Workbook.Worksheets[0]; // assuming you want to read the first worksheet

        int rowCount = worksheet.Dimension.Rows;
        int colCount = worksheet.Dimension.Columns;

        List<BillImportModel> bills = [];

        for (int row = 2; row <= rowCount; row++)
        {
            if (String.IsNullOrEmpty(worksheet.Cells[row, 1].Value.ToString()))
                break;

            decimal paidAmount = decimal.Parse(worksheet.Cells[row, 8].Value.ToString()!);
            var date = DateTime.MinValue;
            try
            {
                date = worksheet.Cells[row, 1].GetValue<DateTime>().ToUniversalTime();
            }
            catch (Exception)
            {
                try
                {
                    date = ParseDate(worksheet.Cells[row, 1].Value.ToString()!);
                }
                catch (Exception ex)
                {
                    throw new Exception($"{ex.Message} - {row}");
                }
            }
            
            var bill = new BillImportModel()
            {
                Date = date,
                Description = worksheet.Cells[row, 2].Value.ToString(),
                CurrencyCode = worksheet.Cells[row, 3].Value.ToString()?.ToUpper() ?? "UAH",
                PaymentCurrencyCode = worksheet.Cells[row, 3].Value.ToString()?.ToUpper() ?? "UAH",
                TotalWithTips = paidAmount,
                Lines = [],
                Payments =
                [
                    new BillPaymentImportModel()
                    {
                        Amount = paidAmount,
                        UserId = creator
                    }
                ]
            };

            var line = new BillLineImportModel()
            {
                ItemDescription = bill.Description,
                Subtotal = paidAmount,
                Participants = []
            };
            for (int col = 9; col <= colCount; col++)
            {
                if (String.IsNullOrEmpty(worksheet.Cells[1, col].Value?.ToString()))
                    continue;

                if (String.IsNullOrEmpty(worksheet.Cells[row, col].Value?.ToString()))
                    continue;

                decimal ratio = decimal.Parse(worksheet.Cells[row, col].Value.ToString()!);

                line.Participants.Add(new BillLineParticipantImportModel()
                {
                    UserId = users[col].Id,
                    Part = ratio
                });

                if (line.Participants.Any(t => t.Part >= 1000000m))
                {
                    line.Participants.ForEach(t => t.Part = t.Part / 10000.0m);
                }
            }

            bill.Lines.Add(line);

            bills.Add(bill);
        }

        return bills;
    }

    private DateTime ParseDate(string value)
    {
        string[] formats = {"dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy", "dd.MM.yyyy HH:mm" };

        DateTime dateValue;

        foreach (string dateStringFormat in formats)
        {
            if (DateTime.TryParseExact(value, dateStringFormat,
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.AdjustToUniversal,
                                       out dateValue))
                
                return dateValue;
        }

        throw new Exception($"Format not detected - {value}");
    }
}
