using System;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Provider;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Service;

public interface IRequestStatusChecker
{
    Task StartAsync();
}

public class RequestStatusChecker : IRequestStatusChecker, ISingletonDependency
{
    private readonly IObjectMapper _objectMapper;
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<RequestStatusChecker> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ICrossChainRequestProvider _crossChainProvider;

    public RequestStatusChecker(ILogger<RequestStatusChecker> logger, ICrossChainRequestProvider crossChainProvider,
        IStorageProvider storageProvider, IObjectMapper objectMapper, IBackgroundJobManager backgroundJobManager)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _storageProvider = storageProvider;
        _crossChainProvider = crossChainProvider;
        _backgroundJobManager = backgroundJobManager;
    }

    public async Task StartAsync()
    {
        _logger.LogInformation("[RequestStatusChecker] Starting RequestStatusChecker ....");
        try
        {
            var results = await _storageProvider.GetFilteredAsync<CrossChainDataDto>(
                RedisKeyConstants.CrossChainDataKey,
                t => t.State is not (CrossChainState.Committed and CrossChainState.Confirmed));
            if (results == null) return;

            _logger.LogDebug($"[RequestStatusChecker] Found {results.Count()} tasks that need to be restarted");

            await Task.WhenAll(results.Select(RestartCrossChainRequestAsync));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[RequestStatusChecker] Starting RequestStatusChecker failed");
        }
    }

    private async Task RestartCrossChainRequestAsync(CrossChainDataDto data)
    {
        try
        {
            var hangfireJobId = await _backgroundJobManager.EnqueueAsync(
                _objectMapper.Map<CrossChainDataDto, CrossChainRequestStartArgs>(data));

            _logger.LogDebug(
                $"[RequestStatusChecker] Scheduler message execute. messageId {data.ReportContext.MessageId}, reqState:{data.State}, hangfireId:{hangfireJobId}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[RequestStatusChecker] Reset cross chain job failed.");
        }
    }
}