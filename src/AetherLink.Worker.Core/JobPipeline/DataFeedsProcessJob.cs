using System;
using System.Threading.Tasks;
using AetherLink.Contracts.DataFeeds.Coordinator;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.Provider;
using Hangfire;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.JobPipeline;

public class DataFeedsProcessJob : AsyncBackgroundJob<DataFeedsProcessJobArgs>, ITransientDependency
{
    private readonly RetryProvider _retryProvider;
    private readonly ILogger<DataFeedsProcessJob> _logger;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;

    public DataFeedsProcessJob(IRecurringJobManager recurringJobManager, ILogger<DataFeedsProcessJob> logger,
        IOracleContractProvider oracleContractProvider, RetryProvider retryProvider)
    {
        _logger = logger;
        _retryProvider = retryProvider;
        _recurringJobManager = recurringJobManager;
        _oracleContractProvider = oracleContractProvider;
    }

    public override async Task ExecuteAsync(DataFeedsProcessJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var argId = IdGeneratorHelper.GenerateId(chainId, reqId);

        try
        {
            _logger.LogInformation("[DataFeeds] Get a new Datafeed job {name} at blockHeight:{blockHeight}.",
                argId, args.BlockHeight);

            var jobSpecStr = await GetSpecAsync(chainId, args.TransactionId);
            var dataFeedsDto = JsonConvert.DeserializeObject<DataFeedsDto>(jobSpecStr);
            if (dataFeedsDto == null)
            {
                _logger.LogWarning("[DataFeeds] {name} Invalid Job spec: {spec}.", argId, jobSpecStr);
                return;
            }

            args.JobSpec = jobSpecStr;
            args.DataFeedsDto = dataFeedsDto;
            args.Epoch = await _oracleContractProvider.GetStartEpochAsync(args.ChainId, args.BlockHeight);

            _logger.LogInformation("[DataFeeds] {name} Start a data feeds timer.", argId);
            _recurringJobManager.AddOrUpdate<DataFeedsTimerProvider>(IdGeneratorHelper.GenerateId(chainId, reqId),
                timer => timer.ExecuteAsync(args), () => dataFeedsDto.Cron);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("[DataFeeds] Get Datafeed job info {name} timeout, retry later.", argId);
            await _retryProvider.RetryAsync(args, untilFailed: true, backOff: true);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DataFeeds] Get a new Datafeed job {name} failed.", argId);
        }
    }

    private async Task<string> GetSpecAsync(string chainId, string transactionId) => SpecificData.Parser.ParseFrom(
            (await _oracleContractProvider.GetRequestCommitmentByTxAsync(chainId, transactionId)).SpecificData)
        .Data.ToStringUtf8();
}