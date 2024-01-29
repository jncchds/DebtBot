using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Models;
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
	
	public void AddBill(BillModel billModel)
	{
		var bill = _mapper.Map<Bill>(billModel);

		_debtContext.Bills.Add(bill);
		_debtContext.SaveChanges();
	}
}