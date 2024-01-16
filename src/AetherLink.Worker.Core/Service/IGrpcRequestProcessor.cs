using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.JobPipeline.Args;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Service;

public interface IGrpcRequestProcessor
{
    Task RequestJobAsync(RequestJob requestJob);
    Task RequestDataMessageAsync(DataMessage dataMessage);
    Task RequestReportAsync(Observations report);
    Task RequestReportSignatureAsync(ReportSignature reportSignature);
    Task RequestTransactionResulAsync(TransactionResult result);
}

public class GrpcRequestProcessor : IGrpcRequestProcessor, ISingletonDependency
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<GrpcRequestProcessor> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;


    public GrpcRequestProcessor(IBackgroundJobManager backgroundJobManager, ILogger<GrpcRequestProcessor> logger,
        IObjectMapper objectMapper)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task RequestJobAsync(RequestJob requestJob)
    {
        _logger.LogDebug("[OCR] Get leader observation request");
        await _backgroundJobManager.EnqueueAsync(new FollowerObservationProcessJobArgs
        {
            RequestId = requestJob.RequestId,
            ChainId = requestJob.ChainId,
            RoundId = requestJob.RoundId,
            Epoch = requestJob.Epoch,
            RequestStartTime = requestJob.StartTime,
        });
    }

    public async Task RequestDataMessageAsync(DataMessage dataMessage)
    {
        _logger.LogDebug("[OCR] Get follower observation response result.");

        await _backgroundJobManager.EnqueueAsync(
            _objectMapper.Map<DataMessage, LeaderGenerateReportJobArgs>(dataMessage));
    }

    public async Task RequestReportAsync(Observations report)
    {
        _logger.LogDebug("[OCR] Get leader report result.");

        await _backgroundJobManager.EnqueueAsync(new FollowerReportProcessJobArgs
        {
            Epoch = report.Epoch,
            ChainId = report.ChainId,
            RoundId = report.RoundId,
            RequestId = report.RequestId,
            Observations = report.ObservationResults.ToList(),
            ReportStartSignTime = report.StartTime
        });
    }

    public async Task RequestReportSignatureAsync(ReportSignature reportSignature)
    {
        _logger.LogDebug("[OCR] Get follower report partialSignature.");

        var args = _objectMapper.Map<ReportSignature, LeaderPartialSigProcessJobArgs>(reportSignature);
        args.PartialSignature = new PartialSignatureDto
        {
            Signature = reportSignature.Signature.ToByteArray(),
            Index = reportSignature.Index
        };
        await _backgroundJobManager.EnqueueAsync(args);
    }

    public async Task RequestTransactionResulAsync(TransactionResult result)
    {
        _logger.LogDebug("[OCR] Get leader transaction result.");

        await _backgroundJobManager.EnqueueAsync(_objectMapper.Map<TransactionResult, FinishedProcessJobArgs>(result),
            delay: TimeSpan.FromSeconds(3));
    }
}