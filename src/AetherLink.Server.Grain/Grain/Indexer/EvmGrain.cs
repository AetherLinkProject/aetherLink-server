using AetherLink.Indexer;
using AetherLink.Indexer.Constants;
using AetherLink.Indexer.Dtos;
using AetherLink.Indexer.Provider;
using AetherLink.Server.Grains.State;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace AetherLink.Server.Grains.Grain.Indexer;

public interface IEvmGrain : IGrainWithStringKey
{
    Task UpdateLatestBlockHeightAsync();
    Task<GrainResultDto<List<EvmChainGrainDto>>> GetBlockHeightAsync();
    Task<GrainResultDto<List<EvmRampRequestGrainDto>>> SearchEvmRequestsAsync(string network, long to, long from);
}

public class EvmGrain : Grain<EvmState>, IEvmGrain
{
    private readonly ILogger<EvmGrain> _logger;
    private readonly IEvmIndexerProvider _indexer;
    private readonly EvmContractsOptions _options;

    public EvmGrain(IEvmIndexerProvider indexer, ILogger<EvmGrain> logger,
        IOptionsSnapshot<EvmContractsOptions> options)
    {
        _logger = logger;
        _indexer = indexer;
        _options = options.Value;
    }

    public async Task UpdateLatestBlockHeightAsync()
    {
        if (string.IsNullOrEmpty(State.Id)) State.Id = this.GetPrimaryKeyString();

        var currentState = new List<EvmChainGrainDto>();
        foreach (var op in _options.ContractConfig.Values)
        {
            var tempWeb3 = new Web3(op.Api);
            var latestBlockHeight = await _indexer.GetLatestBlockHeightAsync(tempWeb3);
            currentState.Add(new()
            {
                NetworkName = op.NetworkName,
                ConsumedBlockHeight = latestBlockHeight
            });
        }

        foreach (var item in currentState)
        {
            _logger.LogDebug($"Updated {item.NetworkName} index height to {item.ConsumedBlockHeight}");
        }

        State.ChainItems = currentState;
        await WriteStateAsync();
    }

    public async Task<GrainResultDto<List<EvmChainGrainDto>>> GetBlockHeightAsync()
        => new() { Success = true, Data = State.ChainItems };

    public async Task<GrainResultDto<List<EvmRampRequestGrainDto>>> SearchEvmRequestsAsync(string network, long to,
        long from)
    {
        if (!_options.ContractConfig.TryGetValue(network, out var op))
        {
            _logger.LogError($"Not exist network {network}");
        }

        var pendingRequests = new List<EvmRampRequestGrainDto>();
        var web3 = new Web3(op.Api);
        for (var curFrom = from; curFrom <= to; curFrom += NetworkConstants.SubscribeBlockStep)
        {
            var currentTo = Math.Min(curFrom + NetworkConstants.SubscribeBlockStep - 1, to);

            try
            {
                var request = await _indexer.GetEvmLogsAsync(web3, op.ContractAddress, curFrom, currentTo);
                pendingRequests.AddRange(request.Select(TryToDecodeFilterLog));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"[EvmSearchWorker] {network} Error processing blocks {curFrom} to {currentTo}: {ex.Message}");
                throw;
            }
        }

        return new() { Success = true, Data = pendingRequests };
    }


    private EvmRampRequestGrainDto TryToDecodeFilterLog(FilterLog log)
    {
        try
        {
            // Check only cross chain request send events
            var decodedSendEvent = Event<SendEventDTO>.DecodeEvent(log);
            if (decodedSendEvent != null)
            {
                return new()
                {
                    TransactionId = log.TransactionHash,
                    MessageId = ByteString.CopyFrom(decodedSendEvent.Event.MessageId).ToBase64(),
                    TargetChainId = (long)decodedSendEvent.Event.TargetChainId,
                    SourceChainId = (long)decodedSendEvent.Event.SourceChainId,
                    Type = CrossChainTransactionType.CrossChainSend
                };
            }

            var decodedForward = Event<ForwardMessageCalledEventDTO>.DecodeEvent(log);
            if (decodedForward != null)
            {
                return new()
                {
                    TransactionId = log.TransactionHash,
                    MessageId = decodedForward.Event.MessageId.ToHex(),
                    TargetChainId = (long)decodedForward.Event.TargetChainId,
                    SourceChainId = (long)decodedForward.Event.SourceChainId,
                    Type = CrossChainTransactionType.CrossChainReceive
                };
            }

            _logger.LogWarning(
                $"[EvmSearchWorkerProvider] Failed to decode event to sendEvent {log.TransactionHash} at {log.BlockNumber}");

            return new();
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[EvmSearchWorkerProvider] Decode {log.TransactionHash} fail at {log.BlockNumber}.");
            throw;
        }
    }
}