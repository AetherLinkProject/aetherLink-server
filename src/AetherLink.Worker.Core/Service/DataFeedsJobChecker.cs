using System;
using System.Linq;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Newtonsoft.Json;
using AetherLink.Worker.Core.Provider;
using Hangfire;
using Hangfire.Storage;
using AetherLink.Worker.Core.Common;
using System.Collections.Generic;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Options;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.Service;

public interface IDataFeedsJobChecker
{
    Task StartAsync();
}

public class DataFeedsJobChecker : IDataFeedsJobChecker, ISingletonDependency
{
    private readonly IObjectMapper _mapper;
    private readonly CheckerOptions _options;
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<DataFeedsJobChecker> _logger;
    private readonly IRecurringJobManager _recurringJobManager;

    public DataFeedsJobChecker(ILogger<DataFeedsJobChecker> logger, IStorageProvider storageProvider,
        IRecurringJobManager recurringJobManager, IOptionsSnapshot<CheckerOptions> options, IObjectMapper mapper)
    {
        _logger = logger;
        _options = options.Value;
        _storageProvider = storageProvider;
        _recurringJobManager = recurringJobManager;
        _mapper = mapper;
    }

    public async Task StartAsync()
    {
        // if (!_options.EnableJobChecker)
        // {
        //     _logger.LogInformation("[DataFeedsJobChecker] JobChecker is disabled. Skipping execution.");
        //     return;
        // }
        // await MockJobs();

        var jobsBefore = JobStorage.Current.GetConnection().GetRecurringJobs();
        _logger.LogInformation($"[DataFeedsJobChecker] Recurring jobs before restart: {jobsBefore.Count}");
        _logger.LogInformation("[DataFeedsJobChecker] Starting DataFeedsJobChecker ....");
        try
        {
            var results = await _storageProvider.GetFilteredAsync<JobDto>(
                RedisKeyConstants.JobKey, t => t.State != RequestState.RequestCanceled);

            _logger.LogDebug($"[DataFeedsJobChecker] Found {results.Count()} DataFeeds jobs that need to be restarted");

            await ProcessJobsSequentiallyAsync(results, RestartDataFeedsJobAsync);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DataFeedsJobChecker] Starting DataFeedsJobChecker failed");
        }

        var jobsAfter = JobStorage.Current.GetConnection().GetRecurringJobs();
        _logger.LogInformation($"[DataFeedsJobChecker] Recurring jobs after restart: {jobsAfter.Count}");
    }

    private async Task ProcessJobsSequentiallyAsync(IEnumerable<JobDto> jobs, Func<JobDto, Task> processor)
    {
        foreach (var job in jobs)
        {
            try
            {
                await processor(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[DataFeedsJobChecker] Error processing job {job.RequestId}");
            }

            await Task.Delay(_options.ProcessDelay);
        }
    }

    private async Task RestartDataFeedsJobAsync(JobDto job)
    {
        try
        {
            var dataFeedsDto = JsonConvert.DeserializeObject<DataFeedsDto>(job.JobSpec);

            var args = _mapper.Map<JobDto, DataFeedsProcessJobArgs>(job);
            args.DataFeedsDto = dataFeedsDto;

            var recurringId = IdGeneratorHelper.GenerateId(job.ChainId, job.RequestId);
            _recurringJobManager.RemoveIfExists(recurringId);
            _logger.LogInformation($"[DataFeedsJobChecker] Removed recurring job: {recurringId}");

            _recurringJobManager.AddOrUpdate<DataFeedsTimerProvider>(
                recurringId, timer => timer.ExecuteAsync(args), () => dataFeedsDto.Cron
            );

            _logger.LogDebug(
                $"[DataFeedsJobChecker] DataFeeds recurring job {recurringId} restart. reqId {job.RequestId}, cron:{dataFeedsDto.Cron}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DataFeedsJobChecker] Reset DataFeeds recurring job failed.");
        }
    }

    private async Task MockJobs()
    {
        // 3 需要重启的任务
        for (int i = 0; i < 3; i++)
        {
            var job = new JobDto
            {
                ChainId = "mock-chain",
                RequestId = $"restart-{Guid.NewGuid()}",
                State = RequestState.RequestStart,
                RequestReceiveTime = DateTime.UtcNow.AddMinutes(-i),
                TransactionBlockTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                JobSpec = JsonConvert.SerializeObject(new DataFeedsDto
                {
                    Cron = "*/5 * * * *",
                    DataFeedsJobSpec = new DataFeedsJobSpec
                    {
                        Type = DataFeedsType.PriceFeeds,
                        CurrencyPair = "BTC/USDT",
                        Url = "http://mock"
                    }
                })
            };
            var key = IdGeneratorHelper.GenerateId(RedisKeyConstants.JobKey, job.ChainId, job.RequestId);
            await _storageProvider.SetAsync(key, job);
        }
        // 1 已取消的任务
        var canceledJob = new JobDto
        {
            ChainId = "mock-chain",
            RequestId = $"canceled-{Guid.NewGuid()}",
            State = RequestState.RequestCanceled,
            RequestReceiveTime = DateTime.UtcNow.AddMinutes(-10),
            TransactionBlockTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            JobSpec = JsonConvert.SerializeObject(new DataFeedsDto
            {
                Cron = "*/10 * * * *",
                DataFeedsJobSpec = new DataFeedsJobSpec
                {
                    Type = DataFeedsType.PriceFeeds,
                    CurrencyPair = "ETH/USDT",
                    Url = "http://mock"
                }
            })
        };
        var canceledKey = IdGeneratorHelper.GenerateId(RedisKeyConstants.JobKey, canceledJob.ChainId, canceledJob.RequestId);
        await _storageProvider.SetAsync(canceledKey, canceledJob);
        _logger.LogInformation("[DataFeedsJobChecker] Mock jobs inserted: 3 restart, 1 canceled.");
    }
}