using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Provider;

public interface IRequestJob
{
    int RequestTypeIndex { get; }
    Task EnqueueAsync(OcrLogEventDto job);
}

public abstract class RequestJobHandler : IRequestJob
{
    public abstract int RequestTypeIndex { get; }
    protected readonly IObjectMapper ObjectMapper;
    protected readonly IBackgroundJobManager BackgroundJobManager;

    protected RequestJobHandler(IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper)
    {
        BackgroundJobManager = backgroundJobManager;
        ObjectMapper = objectMapper;
    }

    public abstract Task EnqueueAsync(OcrLogEventDto job);
}

public class DataFeedRequestJobHandler : RequestJobHandler, ISingletonDependency
{
    public override int RequestTypeIndex => RequestTypeConst.Datafeeds;

    public DataFeedRequestJobHandler(IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper) : base(
        backgroundJobManager, objectMapper)
    {
    }

    public override async Task EnqueueAsync(OcrLogEventDto job)
        => await BackgroundJobManager.EnqueueAsync(ObjectMapper.Map<OcrLogEventDto, DataFeedsProcessJobArgs>(job));
}

public class VrfRequestJobHandler : RequestJobHandler, ISingletonDependency
{
    public override int RequestTypeIndex => RequestTypeConst.Vrf;

    public VrfRequestJobHandler(IBackgroundJobManager backgroundJobManager, IObjectMapper objectMapper) : base(
        backgroundJobManager, objectMapper)
    {
    }

    public override async Task EnqueueAsync(OcrLogEventDto job)
        => await BackgroundJobManager.EnqueueAsync(ObjectMapper.Map<OcrLogEventDto, VRFJobArgs>(job));
}