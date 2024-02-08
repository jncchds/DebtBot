﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Interfaces.Services;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.BillLineParticipant;
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
		var bill = _debtContext
            .Bills
            .Where(q => q.Id == id)
            .Include(q => q.Lines)
            .ThenInclude(q => q.Participants)
            .ThenInclude(q => q.User)
            .Include(q => q.Payments)
            .ThenInclude(q => q.User)
            .ProjectTo<BillModel>(_mapper.ConfigurationProvider)
			.FirstOrDefault();

		return bill;
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

	public Guid AddBill(Guid userId, string billString)
	{
		BillCreationModel billModel = ParseBill(userId, billString);
		return AddBill(billModel);
    }

	private (int index, string val) whitespaceLocator(string w) 
		=> (index: w.IndexOfAny([' ', '\n', '\t', '\v', '\r']), val: w);

    private BillCreationModel ParseBill(Guid userId, string billString)
    {
		var billModel = new BillCreationModel()
		{
			CreatorId = userId,
			Date = DateTime.UtcNow
		};

        var sections = billString.Split("\n\n");
		
		// description
		billModel.Description = sections[0];

		// summary
		var summarySection = sections[1].Split("\n");

		billModel.Total = decimal.Parse(summarySection[0]);
		billModel.CurrencyCode = summarySection[1];
		if(summarySection.Length > 2)
		{
            billModel.PaymentCurrencyCode = summarySection[2];
        }
		else
		{
			billModel.PaymentCurrencyCode = billModel.CurrencyCode;
		}

        // payments
        var paymentsSection = sections[2].Split("\n");
		billModel.Payments = paymentsSection
			.Select(whitespaceLocator)
			.Select(q => new BillPaymentModel
			{
				Amount = decimal.Parse(q.val.Substring(0, q.index)),
				UserId = DetectUser(userId, q.val.Substring(q.index + 1)) ?? Guid.Empty
			})
			.ToList();

		// lines
		var linesSections = sections.Skip(3);
		billModel.Lines = linesSections
            .Select(q => q.Split("\n"))
            .Select(q => new BillLineCreationModel
            {
                ItemDescription = q[0],
                Subtotal = decimal.Parse(q[1]),
                Participants = q.Skip(2)
                                .Select(whitespaceLocator)
                                .Select(w => new BillLineParticipantCreationModel
                                {
                                    Part = decimal.Parse(w.val.Substring(0, w.index)),
                                    UserId = DetectUser(userId, w.val.Substring(w.index + 1)) ?? Guid.Empty
                                })
                                .ToList()
            })
            .ToList();

		return billModel;
    }

	private Guid? DetectUser(Guid userId, string strings)
	{
		var user = _debtContext
			.UserContactsLinks
			.Include(u => u.ContactUser)
			.Where(u => u.UserId == userId)
			.Where(u =>
				u.DisplayName == strings
				|| u.ContactUser.DisplayName == strings
				|| u.ContactUserId.ToString() == strings
				|| (u.ContactUser.TelegramId ?? 0).ToString() == strings
				|| u.ContactUser.Phone == strings
				|| u.ContactUser.Email == strings)
			.Select(u => u.ContactUser)
			.FirstOrDefault();

		if (user == null)
		{
			user = _debtContext
				.Users
				.FirstOrDefault(u =>
					u.DisplayName == strings
					|| u.Id.ToString() == strings
					|| (u.TelegramId ?? 0).ToString() == strings
					|| u.Phone == strings
					|| u.Email == strings);
		}

		return user?.Id;
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

    public List<BillModel> GetByUser(Guid userId)
    {
        
        var bills = _debtContext
            .Bills
            .Include(q => q.Lines)
            .ThenInclude(q => q.Participants)
            .ThenInclude(q => q.User)
            .Include(q => q.Payments)
            .ThenInclude(q => q.User)
            .Where(q => q.CreatorId == userId || q.Payments.Any(w => w.UserId == userId) || q.Lines.Any(w => w.Participants.Any(e => e.UserId == userId)))
            .ProjectTo<BillModel>(_mapper.ConfigurationProvider)
            .ToList();
        return bills;
    }
}