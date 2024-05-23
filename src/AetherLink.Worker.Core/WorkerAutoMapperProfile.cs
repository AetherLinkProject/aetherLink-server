using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AutoMapper;

namespace AetherLink.Worker.Core;

public class AetherLinkServerWorkerAutoMapperProfile : Profile
{
    public AetherLinkServerWorkerAutoMapperProfile()
    {
        CreateMap<OcrLogEventDto, DataFeedsProcessJobArgs>();
        CreateMap<TransmittedDto, TransmittedEventProcessJobArgs>();
        CreateMap<RequestCancelledDto, RequestCancelProcessJobArgs>();

        CreateMap<DataFeedsProcessJobArgs, RequestStartProcessJobArgs>();
        CreateMap<RequestStartProcessJobArgs, JobDto>()
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

        // VRF
        CreateMap<OcrLogEventDto, VRFJobArgs>();
        CreateMap<VRFJobArgs, VrfTxResultCheckJobArgs>();
        CreateMap<VrfTxResultCheckJobArgs, VRFJobArgs>();
        CreateMap<VRFJobArgs, VrfJobDto>();

        CreateMap<JobDto, RequestStartProcessJobArgs>()
            .ForMember(t => t.StartTime, m => m.MapFrom(f => f.TransactionBlockTime));
        CreateMap<JobDto, TransmitResultProcessJobArgs>();
        CreateMap<JobDto, GeneratePartialSignatureJobArgs>();

        // grpc request
        CreateMap<CommitTransmitResultRequest, TransmitResultProcessJobArgs>()
            .ForMember(t => t.TransactionId, m => m.MapFrom(f => f.TransmitTransactionId));
        CreateMap<CommitSignatureRequest, GenerateMultiSignatureJobArgs>();
        CreateMap<CommitObservationRequest, GenerateReportJobArgs>();
    }
}