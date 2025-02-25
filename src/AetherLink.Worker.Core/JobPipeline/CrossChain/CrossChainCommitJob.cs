using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AetherLink.Worker.Core.ChainHandler;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.Dtos;
using AetherLink.Worker.Core.JobPipeline.Args;
using AetherLink.Worker.Core.PeerManager;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace AetherLink.Worker.Core.JobPipeline.CrossChain;

public class CrossChainCommitJob : AsyncBackgroundJob<CrossChainCommitJobArgs>, ITransientDependency
{
    private readonly IPeerManager _peerManager;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<CrossChainCommitJob> _logger;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly Dictionary<long, IChainWriter> _chainWriters;

    public CrossChainCommitJob(IBackgroundJobManager backgroundJobManager, ILogger<CrossChainCommitJob> logger,
        IObjectMapper objectMapper, IPeerManager peerManager, IEnumerable<IChainWriter> chainWriter)
    {
        _logger = logger;
        _peerManager = peerManager;
        _objectMapper = objectMapper;
        _backgroundJobManager = backgroundJobManager;
        _chainWriters = chainWriter.GroupBy(x => x.ChainId).Select(g => g.First())
            .ToDictionary(x => x.ChainId, y => y);
    }

    public override async Task ExecuteAsync(CrossChainCommitJobArgs args)
    {
        var reportContext = args.ReportContext;
        if (!_chainWriters.TryGetValue(reportContext.TargetChainId, out var writer))
        {
            _logger.LogWarning($"[CrossChain][Leader] Unknown target chain id: {reportContext.TargetChainId}");
            return;
        }

        var transactionId =
            await TryToSendTransactionAsync(writer, reportContext, args.PartialSignatures, args.CrossChainData);
        if (string.IsNullOrEmpty(transactionId))
        {
            _logger.LogError($"[CrossChain][Leader] Commit {reportContext.MessageId} report failed");
            return;
        }

        _logger.LogInformation(
            $"[CrossChain][Leader] Commit {reportContext.MessageId} report success, transaction id: {transactionId}.");

        var finishArgs = _objectMapper.Map<CrossChainCommitJobArgs, CrossChainReceivedResultCheckJobArgs>(args);
        finishArgs.CommitTransactionId = transactionId;
        await _backgroundJobManager.EnqueueAsync(finishArgs);

        var txResult = _objectMapper.Map<CrossChainCommitJobArgs, CrossChainReceivedResult>(args);
        txResult.CommitTransactionId = transactionId;
        await _peerManager.BroadcastAsync(p => p.CrossChainReceivedResultCheckAsync(txResult));
    }

    private async Task<string> TryToSendTransactionAsync(IChainWriter writer, ReportContextDto reportContext,
        Dictionary<int, byte[]> partialSignatures, CrossChainDataDto crossChainData)
    {
        // for (var i = 0; i < RetryConstants.DefaultDelay; i++)
        // {
        //     _logger.LogDebug(
        //         $"[CrossChain][Leader] Get message ready to send, MateData: {JsonSerializer.Serialize(reportContext)},CrossChainDataDto: {JsonSerializer.Serialize(crossChainData)} Signature: {string.Join(", ", partialSignatures.Select(kvp => $"Key: {kvp.Key}, Value: {Convert.ToBase64String(kvp.Value)}"))}");
        //
        //     var transactionId =
        //         await writer.SendCommitTransactionAsync(reportContext, partialSignatures, crossChainData);
        //     if (!string.IsNullOrEmpty(transactionId)) return transactionId;
        //
        //     _logger.LogWarning(
        //         $"[CrossChain][Leader] {reportContext.MessageId} send transaction failed in {i} times, will send it later.");
        //     Thread.Sleep((i + 1) * 1000 * 2);
        // }

        return await writer.SendCommitTransactionAsync(reportContext, partialSignatures, crossChainData);
    }
}