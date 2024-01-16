using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AutoMapper;

namespace AetherLink.Worker.Core;

public class AetherLinkServerWorkerAutoMapperProfile : Profile
{
    public AetherLinkServerWorkerAutoMapperProfile()
    {
        CreateMap<OcrLogEventDto, VRFJobArgs>();
        CreateMap<OcrLogEventDto, DataFeedsProcessJobArgs>();
        CreateMap<OcrLogEventDto, TransmittedProcessJobArgs>();
        CreateMap<RequestDto, RequestStartProcessJobArgs>()
            .ForMember(t => t.StartTime, m => m.MapFrom(f => f.TransactionBlockTime));
        CreateMap<DataFeedsProcessJobArgs, RequestStartProcessJobArgs>();
        CreateMap<RequestStartProcessJobArgs, RequestDto>()
            .ForMember(t => t.TransactionBlockTime, m => m.MapFrom(f => f.StartTime));
        CreateMap<OcrLogEventDto, RequestCancelProcessJobArgs>();
        CreateMap<LeaderPartialSigProcessJobArgs, FinishedProcessJobArgs>();
        CreateMap<RequestDto, FinishedProcessJobArgs>();

        CreateMap<FollowerObservationProcessJobArgs, DataMessageDto>();
        CreateMap<FollowerObservationProcessJobArgs, LeaderGenerateReportJobArgs>();
        CreateMap<FollowerObservationProcessJobArgs, DataMessage>();

        CreateMap<FollowerReportProcessJobArgs, LeaderPartialSigProcessJobArgs>();
        CreateMap<FollowerReportProcessJobArgs, ReportSignature>();

        CreateMap<LeaderGenerateReportJobArgs, FollowerReportProcessJobArgs>();
        CreateMap<LeaderGenerateReportJobArgs, Observations>();

        CreateMap<RequestStartProcessJobArgs, FollowerObservationProcessJobArgs>();

        CreateMap<LeaderPartialSigProcessJobArgs, TransactionResult>();

        // grpc request
        CreateMap<TransactionResult, FinishedProcessJobArgs>()
            .ForMember(t => t.TransactionId, m => m.MapFrom(f => f.TransmitTransactionId));
        CreateMap<ReportSignature, LeaderPartialSigProcessJobArgs>();
        CreateMap<DataMessage, LeaderGenerateReportJobArgs>();
    }
}