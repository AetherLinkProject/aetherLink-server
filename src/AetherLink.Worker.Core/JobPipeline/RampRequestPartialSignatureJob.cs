using System.Threading.Tasks;
using AetherLink.Worker.Core.JobPipeline.Args;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline;

public class RampRequestPartialSignatureJob : AsyncBackgroundJob<RampRequestPartialSignatureJobArgs>,
    ITransientDependency
{
    private readonly ILogger<RampRequestPartialSignatureJob> _logger;

    public RampRequestPartialSignatureJob(ILogger<RampRequestPartialSignatureJob> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync(RampRequestPartialSignatureJobArgs args)
    {
        _logger.LogDebug(
            $"get leader ramp request partial signature request.{args.MessageId} {args.ChainId} {args.Epoch} {args.RoundId}");
        // todo: check message id is exist, RampRequestState.RequestStart or retry
        
        // 
        
        
    }
}