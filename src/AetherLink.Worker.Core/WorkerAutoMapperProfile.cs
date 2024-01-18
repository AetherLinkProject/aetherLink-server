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
        CreateMap<TransmittedDto, TransmittedEventProcessJobArgs>();
        CreateMap<RequestCancelledDto, RequestCancelProcessJobArgs>();

        CreateMap<DataFeedsProcessJobArgs, RequestStartProcessJobArgs>();
        CreateMap<RequestStartProcessJobArgs, RequestDto>()
            .ForMember(t => t.TransactionBlockTime, m => m.MapFrom(f => f.StartTime));
        CreateMap<GenerateMultiSignatureJobArgs, TransmitResultProcessJobArgs>();

        CreateMap<CollectObservationJobArgs, DataMessageDto>();
        CreateMap<CollectObservationJobArgs, GenerateReportJobArgs>();
        CreateMap<CollectObservationJobArgs, CommitObservationRequest>();

        CreateMap<GeneratePartialSignatureJobArgs, GenerateMultiSignatureJobArgs>();
        CreateMap<GeneratePartialSignatureJobArgs, CommitSignatureRequest>();

        CreateMap<GenerateReportJobArgs, GeneratePartialSignatureJobArgs>();

        CreateMap<RequestStartProcessJobArgs, CollectObservationJobArgs>();

        CreateMap<GenerateMultiSignatureJobArgs, CommitTransmitResultRequest>();


        CreateMap<RequestDto, RequestStartProcessJobArgs>()
            .ForMember(t => t.StartTime, m => m.MapFrom(f => f.TransactionBlockTime));
        CreateMap<RequestDto, TransmitResultProcessJobArgs>();
        CreateMap<RequestDto, GeneratePartialSignatureJobArgs>();

        // grpc request
        CreateMap<CommitTransmitResultRequest, TransmitResultProcessJobArgs>()
            .ForMember(t => t.TransactionId, m => m.MapFrom(f => f.TransmitTransactionId));
        CreateMap<CommitSignatureRequest, GenerateMultiSignatureJobArgs>();
        CreateMap<CommitObservationRequest, GenerateReportJobArgs>();
    }
}