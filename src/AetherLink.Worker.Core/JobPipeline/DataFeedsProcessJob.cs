using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
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
    }

    [ExceptionHandler(typeof(OperationCanceledException), TargetType = typeof(DataFeedsProcessJob),
        MethodName = nameof(HandleOperationCanceledException))]
    [ExceptionHandler(typeof(Exception), TargetType = typeof(DataFeedsProcessJob),
        MethodName = nameof(HandleException))]
    public virtual async Task Handle(DataFeedsProcessJobArgs args)
    {
        var reqId = args.RequestId;
        var chainId = args.ChainId;
        var argId = IdGeneratorHelper.GenerateId(chainId, reqId);

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

    private async Task<string> GetSpecAsync(string chainId, string transactionId) => SpecificData.Parser.ParseFrom(
            (await _oracleContractProvider.GetRequestCommitmentByTxAsync(chainId, transactionId)).SpecificData)
        .Data.ToStringUtf8();


    #region Exception Handing

    public async Task<FlowBehavior> HandleOperationCanceledException(Exception ex, DataFeedsProcessJobArgs args)
    {
        var argId = IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId);
        _logger.LogError("[DataFeeds] Get Datafeed job info {name} timeout, retry later.", argId);
        await _retryProvider.RetryAsync(args, untilFailed: true, backOff: true);

        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
        };
    }

    public async Task<FlowBehavior> HandleException(Exception ex, DataFeedsProcessJobArgs args)
    {
        var argId = IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId);
        _logger.LogError(ex, "[DataFeeds] Get a new Datafeed job {name} failed.", argId);
        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
        };
    }

    #endregion
}