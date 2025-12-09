using System;
using System.Threading.Tasks;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Service;

public interface IServiceProcessor
{
    Task ProcessQueryAsync(QueryObservationRequest request, ServerCallContext context);
    Task ProcessObservationAsync(CommitObservationRequest request, ServerCallContext context);
    Task ProcessReportAsync(CommitReportRequest request, ServerCallContext context);
    Task ProcessProcessedReportAsync(CommitSignatureRequest request, ServerCallContext context);
    Task ProcessTransmittedResultAsync(CommitTransmitResultRequest request, ServerCallContext context);
    Task ProcessReportSignatureAsync(QueryReportSignatureRequest request, ServerCallContext context);
    Task ProcessPartialSignatureAsync(CommitPartialSignatureRequest request, ServerCallContext context);
    Task ProcessTransmitResultAsync(BroadcastTransmitResult request, ServerCallContext context);
    Task ProcessMessagePartialSignatureQueryAsync(QueryMessageSignatureRequest request, ServerCallContext context);
    Task ProcessMessagePartialSignatureReturnAsync(ReturnPartialSignatureResults request, ServerCallContext context);
    Task ProcessCrossChainReceivedResultAsync(CrossChainReceivedResult request, ServerCallContext context);
}

public class ServiceProcessor : IServiceProcessor, ISingletonDependency
{
    private readonly TimeSpan _jobDelay;
    private readonly ProcessJobOptions _option;
    private readonly IObjectMapper _objectMapper;
    private readonly IBackgroundJobManager _jobManager;
    private readonly ILogger<ServiceProcessor> _logger;

    public ServiceProcessor(IBackgroundJobManager jobManager, IObjectMapper objectMapper,
        IOptionsSnapshot<ProcessJobOptions> option, ILogger<ServiceProcessor> logger)
    {
        _logger = logger;
        _option = option.Value;
        _jobManager = jobManager;
        _objectMapper = objectMapper;
        _jobDelay = TimeSpan.FromSeconds(_option.DefaultEnqueueDelay);
    }

    public async Task ProcessQueryAsync(QueryObservationRequest request, ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await EnqueueAsync(new CollectObservationJobArgs
        {
            RequestId = request.RequestId,
            ChainId = request.ChainId,
            RoundId = request.RoundId,
            Epoch = request.Epoch,
        });
    }

    public async Task ProcessObservationAsync(CommitObservationRequest request, ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await EnqueueAsync(_objectMapper.Map<CommitObservationRequest, GenerateReportJobArgs>(request));
    }

    public async Task ProcessReportAsync(CommitReportRequest request, ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await EnqueueAsync(new GeneratePartialSignatureJobArgs
        {
            Epoch = request.Epoch,
            ChainId = request.ChainId,
            RoundId = request.RoundId,
            RequestId = request.RequestId,
            Observations = request.ObservationResults
        });
    }

    public async Task ProcessProcessedReportAsync(CommitSignatureRequest request, ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        var args = _objectMapper.Map<CommitSignatureRequest, GenerateMultiSignatureJobArgs>(request);
        args.PartialSignature = new PartialSignatureDto
        {
            Signature = request.Signature.ToByteArray(),
            Index = request.Index
        };
        await EnqueueAsync(args);
    }

    public async Task ProcessTransmittedResultAsync(CommitTransmitResultRequest request, ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await EnqueueAsync(_objectMapper.Map<CommitTransmitResultRequest, TransmitResultProcessJobArgs>(request));
    }

    public async Task ProcessReportSignatureAsync(QueryReportSignatureRequest request, ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await _jobManager.EnqueueAsync(new ReportSignatureRequestArgs
        {
            Context = request.Context,
            Payload = request.Payload.ToByteArray()
        });
    }

    public async Task ProcessPartialSignatureAsync(CommitPartialSignatureRequest request, ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await _jobManager.EnqueueAsync(new PartialSignatureResponseArgs
        {
            Context = request.Context,
            Signature = request.Signature.ToByteArray(),
            Index = request.Index,
            Payload = request.Payload.ToByteArray()
        });
    }

    public async Task ProcessMessagePartialSignatureQueryAsync(QueryMessageSignatureRequest request,
        ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await _jobManager.EnqueueAsync(
            _objectMapper.Map<QueryMessageSignatureRequest, CrossChainPartialSignatureJobArgs>(request));
    }

    public async Task ProcessMessagePartialSignatureReturnAsync(ReturnPartialSignatureResults request,
        ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await _jobManager.EnqueueAsync(
            _objectMapper.Map<ReturnPartialSignatureResults, CrossChainMultiSignatureJobArgs>(request));
    }

    public async Task ProcessCrossChainReceivedResultAsync(CrossChainReceivedResult request, ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await _jobManager.EnqueueAsync(
            _objectMapper.Map<CrossChainReceivedResult, CrossChainReceivedResultCheckJobArgs>(request));
    }

    public async Task ProcessTransmitResultAsync(BroadcastTransmitResult request, ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await _jobManager.EnqueueAsync(new BroadcastTransmitResultArgs
        {
            Context = request.Context,
            TransactionId = request.TransactionId
        });
    }

    // todo: add metadata validate
    private bool ValidateRequest(ServerCallContext context) => true;

    private async Task EnqueueAsync<T>(T arg) where T : JobPipelineArgsBase
        => await _jobManager.EnqueueAsync(arg, delay: _jobDelay);
}