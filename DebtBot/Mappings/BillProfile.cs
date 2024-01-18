using AutoMapper;
using DebtBot.DB.Entities;
using DebtBot.Models;

namespace DebtBot.Mappings;

public class BillProfile : Profile
{
	public BillProfile()
	{
		CreateMap<Bill, BillModel>().ReverseMap();

		CreateMap<BillPayment, BillPaymentModel>().ReverseMap();

		CreateMap<BillLine, BillLineModel>().ReverseMap();

		CreateMap<BillLineParticipant, BillLineParticipantModel>()
			.ForMember(q => q.UserDisplayName, opt => opt.MapFrom(w => w.User.DisplayName));

		CreateMap<BillLineParticipantModel, BillLineParticipant>();
	}
}