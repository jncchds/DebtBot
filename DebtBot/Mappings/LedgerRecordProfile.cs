using AutoMapper;
using DebtBot.DB.Entities;
using DebtBot.Models.LedgerRecord;

namespace DebtBot.Mappings;

public class LedgerRecordProfile : Profile
{
    public LedgerRecordProfile()
    {
        CreateProjection<LedgerRecord, LedgerRecordModel>();
    }
}
