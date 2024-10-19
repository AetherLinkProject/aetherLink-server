using AetherLink.Worker.Core.Automation.Args;
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
        CreateMap<CollectObservationJobArgs, PlainDataFeedsDto>();
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

        // Automation
        CreateMap<OcrLogEventDto, AutomationJobArgs>();
        CreateMap<AutomationJobArgs, AutomationStartJobArgs>();
        CreateMap<QueryReportSignatureRequest, ReportSignatureRequestArgs>();
        CreateMap<AutomationStartJobArgs, JobDto>();
        CreateMap<AutomationJobArgs, JobDto>();
        CreateMap<JobDto, AutomationStartJobArgs>();

        // grpc request
        CreateMap<CommitTransmitResultRequest, TransmitResultProcessJobArgs>()
            .ForMember(t => t.TransactionId, m => m.MapFrom(f => f.TransmitTransactionId));
        CreateMap<CommitSignatureRequest, GenerateMultiSignatureJobArgs>();
        CreateMap<CommitObservationRequest, GenerateReportJobArgs>();

        // Ramp
        CreateMap<RampRequestDto, RampRequestStartJobArgs>();
        CreateMap<RampMessageDto, RampRequestStartJobArgs>();
        CreateMap<RampRequestStartJobArgs, RampMessageDto>();
        CreateMap<RampCommitResultRequest, RampRequestCommitResultJobArgs>();
        CreateMap<RampRequestMultiSignatureJobArgs, RampCommitResultRequest>();
        CreateMap<RampRequestStartJobArgs, RampRequestPartialSignatureJobArgs>();
        CreateMap<ReturnPartialSignatureResults, RampRequestMultiSignatureJobArgs>();
        CreateMap<QueryMessageSignatureRequest, RampRequestPartialSignatureJobArgs>();
        CreateMap<RampRequestMultiSignatureJobArgs, RampRequestCommitResultJobArgs>();
        CreateMap<RampRequestPartialSignatureJobArgs, ReturnPartialSignatureResults>();
        CreateMap<RampRequestPartialSignatureJobArgs, RampRequestMultiSignatureJobArgs>();
    }
}