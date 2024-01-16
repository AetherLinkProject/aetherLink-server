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
        var argsName = IdGeneratorHelper.GenerateId(chainId, reqId);

        try
        {
            _logger.LogInformation("[DataFeedsProcessJob] Get a new Datafeed job {name}.", argsName);

            var commitment = await _oracleContractProvider.GetCommitmentAsync(chainId, args.TransactionId, reqId);
            var specificData = SpecificData.Parser.ParseFrom(commitment.SpecificData);
            var jobSpecStr = specificData.Data.ToStringUtf8();

            _logger.LogDebug("[DataFeedsProcessJob] {name} jobSpec :{specStr}", argsName, jobSpecStr);

            var dataFeedsDto = JsonConvert.DeserializeObject<DataFeedsDto>(jobSpecStr);
            if (dataFeedsDto == null)
            {
                _logger.LogWarning("[DataFeedsProcessJob] {name} Invalid Job spec: {spec}.", argsName, jobSpecStr);
                return;
            }

            args.JobSpec = jobSpecStr;
            args.DataFeedsDto = dataFeedsDto;

            _logger.LogInformation("[DataFeedsProcessJob] {name} Start a data feeds timer.", argsName);
            _recurringJobManager.AddOrUpdate<DataFeedsTimerProvider>(
                IdGeneratorHelper.GenerateId(args.ChainId, args.RequestId), timer => timer.ExecuteAsync(args),
                () => dataFeedsDto.Cron);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[DataFeedsProcessJob] Get a new Datafeed job {name} failed.", argsName);
        }
    }
}