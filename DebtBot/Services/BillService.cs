using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Bill;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.Services;

public class BillService : IBillService
{
	private readonly DebtContext _debtContext;
	private readonly IMapper _mapper;
	
	public BillService(DebtContext debtContext, IMapper mapper)
	{
		_debtContext = debtContext;
		_mapper = mapper;
	}

	public BillModel? Get(Guid id)
	{
		var bill = _debtContext.Bills.FirstOrDefault(q => q.Id == id);
		return _mapper.Map<BillModel>(bill);
	}
	
	public List<BillModel> Get()
	{
		var bills = _debtContext
			.Bills
			.Include(q => q.Lines)
			.ThenInclude(q => q.Participants)
			.ThenInclude(q => q.User)
			.Include(q => q.Payments)
			.ThenInclude(q => q.User)
			.ProjectTo<BillModel>(_mapper.ConfigurationProvider)
			.ToList();
		return bills;
	}
	
	public Guid AddBill(BillCreationModel billModel)
	{
		var bill = _mapper.Map<Bill>(billModel);

		_debtContext.Bills.Add(bill);
		_debtContext.SaveChanges();

		return bill.Id;
	}

    public bool Finalize(Guid id)
    {
		var bill = _debtContext.Bills.FirstOrDefault(q => q.Id == id);
        if (bill == null)
		{
            return false;
        }

		if(bill.Status != DB.Enums.ProcessingState.Draft)
		{
			return false;
		}

        bill.Status = DB.Enums.ProcessingState.Ready;
        _debtContext.SaveChanges();

		return true;
    }

    public bool HasAccess(Guid userId, BillModel? bill)
    {
	    if (bill is null)
	    {
		    return false;
	    }
	    
	    if (bill.CreatorId == userId)
	    {
		    return true;
	    }
	    
	    if(bill.Payments.Any(q => q.UserId == userId))
	    {
		    return true;
	    }

	    if(bill.Lines.Any(q => q.Participants.Any(w => w.UserId == userId)))
	    {
		    return true;
	    }

	    return false;
    }
}