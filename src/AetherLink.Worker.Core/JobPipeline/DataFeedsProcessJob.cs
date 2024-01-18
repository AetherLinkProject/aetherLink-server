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
    private readonly ILogger<DataFeedsProcessJob> _logger;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly IOracleContractProvider _oracleContractProvider;

    public DataFeedsProcessJob(IRecurringJobManager recurringJobManager, ILogger<DataFeedsProcessJob> logger,
        IOracleContractProvider oracleContractProvider)
    {
        _logger = logger;
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
            _logger.LogInformation("[DataFeedsProcessJob] Get a new Datafeed job {name} at blockHeight:{blockHeight}.",
                argId, args.BlockHeight);

            var jobSpecStr = await GetSpecAsync(chainId, args.TransactionId, reqId);
            if (!ValidateDataFeedFormat(jobSpecStr, out var dataFeedsDto))
            {
                _logger.LogWarning("[DataFeedsProcessJob] {name} Invalid Job spec: {spec}.", argId, jobSpecStr);
                return;
            }

            args.JobSpec = jobSpecStr;
            args.DataFeedsDto = dataFeedsDto;
            args.Epoch = await _oracleContractProvider.GetStartEpochAsync(args.ChainId, args.BlockHeight);

            _logger.LogInformation("[DataFeedsProcessJob] {name} Start a data feeds timer.", argId);
            _recurringJobManager.AddOrUpdate<DataFeedsTimerProvider>(IdGeneratorHelper.GenerateId(chainId, reqId),
                timer => timer.ExecuteAsync(args), () => dataFeedsDto.Cron);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DataFeedsProcessJob] Get a new Datafeed job {name} failed.", argId);
        }
    }

    private async Task<string> GetSpecAsync(string chainId, string transactionId, string reqId)
    {
        var commitment =
            await _oracleContractProvider.GetRequestCommitmentAsync(chainId, transactionId, reqId);
        var specificData = SpecificData.Parser.ParseFrom(commitment.SpecificData);
        return specificData.Data.ToStringUtf8();
    }

    private static bool ValidateDataFeedFormat(string spec, out DataFeedsDto dataFeed)
    {
        dataFeed = JsonConvert.DeserializeObject<DataFeedsDto>(spec);
        return dataFeed != null;
    }
}