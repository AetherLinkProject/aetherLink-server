using System;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Options;
using Grpc.Core;
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
}

public class ServiceProcessor : IServiceProcessor, ISingletonDependency
{
    private readonly ProcessJobOptions _option;
    private readonly IObjectMapper _objectMapper;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public ServiceProcessor(IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper,
        IOptionsSnapshot<ProcessJobOptions> option)
    {
        _option = option.Value;
        _objectMapper = objectMapper;
        _backgroundJobManager = backgroundJobManager;
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
            Observations = request.ObservationResults.ToList(),
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

    // todo: add metadata validate
    private bool ValidateRequest(ServerCallContext context) => true;

    private async Task EnqueueAsync<T>(T arg) where T : JobPipelineArgsBase
        => await _backgroundJobManager.EnqueueAsync(arg, delay: TimeSpan.FromSeconds(_option.DefaultEnqueueDelay));
}