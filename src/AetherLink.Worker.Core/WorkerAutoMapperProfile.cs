using AetherLink.Indexer.Dtos;
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

        // CrossChain
        CreateMap<CrossChainRequestStartArgs, CrossChainDataDto>();
        CreateMap<CrossChainDataDto, CrossChainRequestStartArgs>();
        CreateMap<CrossChainPartialSignatureJobArgs, CrossChainMultiSignatureJobArgs>();
        CreateMap<CrossChainMultiSignatureJobArgs, CrossChainCommitJobArgs>();
        CreateMap<CrossChainCommitJobArgs, CrossChainReceivedResultCheckJobArgs>();
        CreateMap<RampRequestCancelledDto, CrossChainRequestCancelJobArgs>();
        CreateMap<RampRequestManuallyExecutedDto, CrossChainRequestManuallyExecuteJobArgs>();
        CreateCrossChainGrpcMap();
    }

    private void CreateCrossChainGrpcMap()
    {
        // CrossChain GRPC
        CreateMap<CrossChainRequestStartArgs, QueryMessageSignatureRequest>()
            .ForPath(t => t.ReportContext.MessageId, m => m.MapFrom(f => f.ReportContext.MessageId))
            .ForPath(t => t.ReportContext.SourceChainId, m => m.MapFrom(f => f.ReportContext.SourceChainId))
            .ForPath(t => t.ReportContext.TargetChainId, m => m.MapFrom(f => f.ReportContext.TargetChainId))
            .ForPath(t => t.ReportContext.Sender, m => m.MapFrom(f => f.ReportContext.Sender))
            .ForPath(t => t.ReportContext.Receiver, m => m.MapFrom(f => f.ReportContext.Receiver))
            .ForPath(t => t.ReportContext.Epoch, m => m.MapFrom(f => f.ReportContext.Epoch))
            .ForPath(t => t.ReportContext.RoundId, m => m.MapFrom(f => f.ReportContext.RoundId));
        CreateMap<QueryMessageSignatureRequest, CrossChainPartialSignatureJobArgs>()
            .ForPath(t => t.ReportContext.MessageId, m => m.MapFrom(f => f.ReportContext.MessageId))
            .ForPath(t => t.ReportContext.SourceChainId, m => m.MapFrom(f => f.ReportContext.SourceChainId))
            .ForPath(t => t.ReportContext.TargetChainId, m => m.MapFrom(f => f.ReportContext.TargetChainId))
            .ForPath(t => t.ReportContext.Sender, m => m.MapFrom(f => f.ReportContext.Sender))
            .ForPath(t => t.ReportContext.Receiver, m => m.MapFrom(f => f.ReportContext.Receiver))
            .ForPath(t => t.ReportContext.Epoch, m => m.MapFrom(f => f.ReportContext.Epoch))
            .ForPath(t => t.ReportContext.RoundId, m => m.MapFrom(f => f.ReportContext.RoundId));
        CreateMap<CrossChainPartialSignatureJobArgs, ReturnPartialSignatureResults>()
            .ForPath(t => t.ReportContext.MessageId, m => m.MapFrom(f => f.ReportContext.MessageId))
            .ForPath(t => t.ReportContext.SourceChainId, m => m.MapFrom(f => f.ReportContext.SourceChainId))
            .ForPath(t => t.ReportContext.TargetChainId, m => m.MapFrom(f => f.ReportContext.TargetChainId))
            .ForPath(t => t.ReportContext.Sender, m => m.MapFrom(f => f.ReportContext.Sender))
            .ForPath(t => t.ReportContext.Receiver, m => m.MapFrom(f => f.ReportContext.Receiver))
            .ForPath(t => t.ReportContext.Epoch, m => m.MapFrom(f => f.ReportContext.Epoch))
            .ForPath(t => t.ReportContext.RoundId, m => m.MapFrom(f => f.ReportContext.RoundId));
        CreateMap<ReturnPartialSignatureResults, CrossChainMultiSignatureJobArgs>()
            .ForPath(t => t.ReportContext.MessageId, m => m.MapFrom(f => f.ReportContext.MessageId))
            .ForPath(t => t.ReportContext.SourceChainId, m => m.MapFrom(f => f.ReportContext.SourceChainId))
            .ForPath(t => t.ReportContext.TargetChainId, m => m.MapFrom(f => f.ReportContext.TargetChainId))
            .ForPath(t => t.ReportContext.Sender, m => m.MapFrom(f => f.ReportContext.Sender))
            .ForPath(t => t.ReportContext.Receiver, m => m.MapFrom(f => f.ReportContext.Receiver))
            .ForPath(t => t.ReportContext.Epoch, m => m.MapFrom(f => f.ReportContext.Epoch))
            .ForPath(t => t.ReportContext.RoundId, m => m.MapFrom(f => f.ReportContext.RoundId));
        CreateMap<CrossChainCommitJobArgs, CrossChainReceivedResult>()
            .ForPath(t => t.ReportContext.MessageId, m => m.MapFrom(f => f.ReportContext.MessageId))
            .ForPath(t => t.ReportContext.SourceChainId, m => m.MapFrom(f => f.ReportContext.SourceChainId))
            .ForPath(t => t.ReportContext.TargetChainId, m => m.MapFrom(f => f.ReportContext.TargetChainId))
            .ForPath(t => t.ReportContext.Sender, m => m.MapFrom(f => f.ReportContext.Sender))
            .ForPath(t => t.ReportContext.Receiver, m => m.MapFrom(f => f.ReportContext.Receiver))
            .ForPath(t => t.ReportContext.Epoch, m => m.MapFrom(f => f.ReportContext.Epoch))
            .ForPath(t => t.ReportContext.RoundId, m => m.MapFrom(f => f.ReportContext.RoundId));
        CreateMap<CrossChainReceivedResult, CrossChainReceivedResultCheckJobArgs>()
            .ForPath(t => t.ReportContext.MessageId, m => m.MapFrom(f => f.ReportContext.MessageId))
            .ForPath(t => t.ReportContext.SourceChainId, m => m.MapFrom(f => f.ReportContext.SourceChainId))
            .ForPath(t => t.ReportContext.TargetChainId, m => m.MapFrom(f => f.ReportContext.TargetChainId))
            .ForPath(t => t.ReportContext.Sender, m => m.MapFrom(f => f.ReportContext.Sender))
            .ForPath(t => t.ReportContext.Receiver, m => m.MapFrom(f => f.ReportContext.Receiver))
            .ForPath(t => t.ReportContext.Epoch, m => m.MapFrom(f => f.ReportContext.Epoch))
            .ForPath(t => t.ReportContext.RoundId, m => m.MapFrom(f => f.ReportContext.RoundId));
        CreateMap<TokenTransferMetadataDto, TokenTransferMetadata>()
            .ForPath(t => t.ExtraData, m => m.MapFrom(f => f.ExtraData))
            .ForPath(t => t.TokenAddress, m => m.MapFrom(f => f.TokenAddress))
            .ForPath(t => t.TargetChainId, m => m.MapFrom(f => f.TargetChainId))
            .ForPath(t => t.Symbol, m => m.MapFrom(f => f.Symbol));
    }
}