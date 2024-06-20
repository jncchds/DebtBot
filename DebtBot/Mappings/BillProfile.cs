using AutoMapper;
using DebtBot.DB.Entities;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.BillLineParticipant;

namespace DebtBot.Mappings;

public class BillProfile : Profile
{
	public BillProfile()
    {
        CreateProjection<Bill, BillListModel>();

        CreateProjection<Bill, BillModel>();

        CreateProjection<BillPayment, BillPaymentModel>();

        CreateProjection<BillLine, BillLineModel>();

        CreateProjection<BillLineParticipant, BillLineParticipantModel>()
            .ForMember(q => q.UserDisplayName, opt => opt.MapFrom(w => w.User.DisplayName));

        CreateProjection<BillLineParticipantModel, BillLineParticipant>();
        

        CreateMap<Bill, BillModel>().ReverseMap();
        CreateMap<BillCreationModel, Bill>();

        CreateMap<BillPayment, BillPaymentModel>().ReverseMap();

        CreateMap<BillPaymentCreationModel, BillPayment>();

		CreateMap<BillLine, BillLineModel>().ReverseMap();
        CreateMap<BillLineCreationModel, BillLine>();

        CreateMap<BillLineParticipant, BillLineParticipantModel>()
			.ForMember(q => q.UserDisplayName, opt => opt.MapFrom(w => w.User.DisplayName));

		CreateMap<BillLineParticipantModel, BillLineParticipant>();
        CreateMap<BillLineParticipantCreationModel, BillLineParticipant>();
	}
}