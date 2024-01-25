using System;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using Grpc.Core;
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
}

public class ServiceProcessor : IServiceProcessor, ISingletonDependency
{
    private readonly IObjectMapper _objectMapper;
    private readonly ProcessJobOptions _processJobOptions;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public ServiceProcessor(IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper,
        ProcessJobOptions processJobOptions)
    {
        _objectMapper = objectMapper;
        _processJobOptions = processJobOptions;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task ProcessQueryAsync(QueryObservationRequest request, ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await _backgroundJobManager.EnqueueAsync(new CollectObservationJobArgs
        {
            RequestId = request.RequestId,
            ChainId = request.ChainId,
            RoundId = request.RoundId,
            Epoch = request.Epoch,
        }, delay: TimeSpan.FromSeconds(_processJobOptions.DefaultEnqueueDelay));
    }

    public async Task ProcessObservationAsync(CommitObservationRequest request, ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<CommitObservationRequest, GenerateReportJobArgs>(request)
            , delay: TimeSpan.FromSeconds(_processJobOptions.DefaultEnqueueDelay));
    }

    public async Task ProcessReportAsync(CommitReportRequest request, ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await _backgroundJobManager.EnqueueAsync(new GeneratePartialSignatureJobArgs
        {
            Epoch = request.Epoch,
            ChainId = request.ChainId,
            RoundId = request.RoundId,
            RequestId = request.RequestId,
            Observations = request.ObservationResults.ToList(),
        }, delay: TimeSpan.FromSeconds(_processJobOptions.DefaultEnqueueDelay));
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
        await _backgroundJobManager.EnqueueAsync(args,
            delay: TimeSpan.FromSeconds(_processJobOptions.DefaultEnqueueDelay));
    }

    public async Task ProcessTransmittedResultAsync(CommitTransmitResultRequest request, ServerCallContext context)
    {
        if (!ValidateRequest(context)) return;

        await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<CommitTransmitResultRequest, TransmitResultProcessJobArgs>(request)
            , delay: TimeSpan.FromSeconds(_processJobOptions.DefaultEnqueueDelay));
    }

    // todo: add metadata validate
    private bool ValidateRequest(ServerCallContext context) => true;
}