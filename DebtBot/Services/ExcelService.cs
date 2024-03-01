﻿using DebtBot.Interfaces.Services;
using DebtBot.Models.Bill;
using DebtBot.Models.User;
using OfficeOpenXml;
using System.Data.SqlTypes;
using System.Globalization;

namespace DebtBot.Services;

public class ExcelService : IExcelService
{
    public IEnumerable<BillParserModel> Import(Stream file)
    {

        using var package = new ExcelPackage(file);

        var worksheet = package.Workbook.Worksheets[0]; // assuming you want to read the first worksheet

        int rowCount = worksheet.Dimension.Rows;
        int colCount = worksheet.Dimension.Columns;

        for (int row = 2; row <= rowCount; row++)
        {
            if (String.IsNullOrEmpty(worksheet.Cells[row, 1].Value.ToString()))
                break;

            decimal paidAmount = decimal.Parse(worksheet.Cells[row, 8].Value.ToString()!);
            var date = DateTime.MinValue;
            try
            {
                date = worksheet.Cells[row, 1].GetValue<DateTime>();
            }
            catch (Exception)
            {
                try
                {
                    date = Parse(worksheet.Cells[row, 1].Value.ToString()!);
                }
                catch (Exception ex)
                {
                    throw new Exception($"{ex.Message} - {row}");
                }
            }
            
            BillParserModel bill = new BillParserModel()
            {
                Date = date,
                Description = worksheet.Cells[row, 2].Value.ToString(),
                CurrencyCode = worksheet.Cells[row, 3].Value.ToString()?.ToUpper() ?? "UAH",
                TotalWithTips = paidAmount,
                Lines = [],
                Payments =
                [
                    new BillPaymentParserModel()
                    {
                        Amount = paidAmount,
                        User = new UserSearchModel() { TelegramUserName = "@jnc_chds" }
                    }
                ]
            };

            var line = new BillLineParserModel()
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

                string user = worksheet.Cells[1, col].Value.ToString()!;
                decimal ratio = decimal.Parse(worksheet.Cells[row, col].Value.ToString()!);

                line.Participants.Add(new BillLineParticipantParserModel()
                {
                    User = new Models.User.UserSearchModel() { DisplayName = user },
                    Part = ratio
                });
            }

            bill.Lines.Add(line);

            yield return bill;
        }
    }

    private DateTime Parse(string value)
    {
        string[] formats = {"dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy", "dd.MM.yyyy HH:mm" };

        DateTime dateValue;

        foreach (string dateStringFormat in formats)
        {
            if (DateTime.TryParseExact(value, dateStringFormat,
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.None,
                                       out dateValue))
                
                return dateValue;
        }

        throw new Exception($"Format not detected - {value}");
    }
}
