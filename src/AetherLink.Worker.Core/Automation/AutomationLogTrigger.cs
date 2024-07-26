using System;
using System.Threading.Tasks;
using AetherLink.Contracts.Automation;
using Aetherlink.PriceServer.Common;
using AetherLink.Worker.Core.Automation.Args;
using AetherLink.Worker.Core.Automation.Providers;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.OCR;
using AetherLink.Worker.Core.PeerManager;
using AetherLink.Worker.Core.Provider;
using AetherLink.Worker.Core.Scheduler;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Automation;

public class AutomationLogTrigger : AsyncBackgroundJob<AutomationLogTriggerArgs>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IStorageProvider _storageProvider;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<AutomationLogTrigger> _logger;
    private readonly ISignatureProvider _signatureProvider;
    private readonly IOracleContractProvider _contractProvider;

    public AutomationLogTrigger(IPeerManager peerManager, ILogger<AutomationLogTrigger> logger,
        ISchedulerService schedulerService, IOracleContractProvider contractProvider,
        ISignatureProvider signatureProvider, IStorageProvider storageProvider)
    {
        _logger = logger;
        _peerManager = peerManager;
        _storageProvider = storageProvider;
        _contractProvider = contractProvider;
        _schedulerService = schedulerService;
        _signatureProvider = signatureProvider;
    }

    public override async Task ExecuteAsync(AutomationLogTriggerArgs args)
    {
        // todo add request canceled filter
        var chainId = args.Context.ChainId;
        var upkeepId = args.Context.RequestId;
        var epoch = args.Context.Epoch;
        var logUpkeepId = args.LogUpkeepStorageId;
        var eventId = args.TransactionEventStorageId;
        var logTriggerId = AutomationHelper.GenerateLogTriggerId(eventId, logUpkeepId);
        var logTriggerKey = AutomationHelper.GenerateLogTriggerKey(eventId, logUpkeepId);

        try
        {
            var startTime = DateTimeOffset.FromUnixTimeMilliseconds(args.StartTime).DateTime;
            var transactionEvent = await _storageProvider.GetAsync<TransactionEventDto>(eventId);
            var logTriggerInfo = await _storageProvider.GetAsync<LogTriggerDto>(logTriggerKey);
            if (logTriggerInfo == null)
            {
                logTriggerInfo = new LogTriggerDto
                {
                    TransactionEventStorageId = eventId,
                    LogUpkeepStorageId = logUpkeepId,
                    ReceiveTime = startTime,
                    Context = args.Context
                };
            }
            else if (logTriggerInfo.Context.RoundId != 0)
            {
                logTriggerInfo.ReceiveTime = _schedulerService.UpdateBlockTime(startTime);
            }
            else
            {
                _logger.LogWarning($"[Automation] {logTriggerId} has been executed.");
                return;
            }

            await _storageProvider.SetAsync(logTriggerKey, logTriggerInfo);

            _logger.LogDebug($"[Automation] Log trigger {logTriggerId} startTime {logTriggerInfo.ReceiveTime}");

            if (_peerManager.IsLeader(logTriggerInfo.Context))
            {
                _logger.LogInformation($"[Automation][Leader] {logTriggerId} Is Leader.");
                var request = new QueryReportSignatureRequest
                {
                    Context = logTriggerInfo.Context,
                    Payload = new LogTriggerCheckData
                    {
                        ChainId = transactionEvent.ChainId,
                        TransactionId = transactionEvent.TransactionId,
                        BlockHeight = transactionEvent.BlockHeight,
                        BlockHash = transactionEvent.BlockHash,
                        ContractAddress = transactionEvent.ContractAddress,
                        EventName = transactionEvent.EventName,
                        Index = transactionEvent.Index
                    }.ToByteString()
                };
                
                

                _signatureProvider.LeaderInitMultiSign(chainId, logTriggerId, _signatureProvider.GenerateMsg(
                        await _contractProvider.GenerateTransmitDataAsync(chainId, upkeepId, epoch, request.Payload))
                    .ToByteArray());
                await _peerManager.BroadcastAsync(p => p.QueryReportSignatureAsync(request));
            }

            _schedulerService.StartScheduler(logTriggerInfo);

            _logger.LogInformation($"[Automation] {logTriggerId} Waiting for request end.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[Automation] {logTriggerId} Start process failed.");
        }
    }
}