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

    public EvmGrain(IOptionsSnapshot<EvmContractsOptions> options, IEvmIndexerProvider indexer,
        ILogger<EvmGrain> logger)
    {
        _logger = logger;
        _indexer = indexer;
        _options = options.Value;
    }

    public async Task UpdateLatestBlockHeightAsync()
    {
        if (string.IsNullOrEmpty(State.Id)) State.Id = this.GetPrimaryKeyString();
        var currentState = State.ChainItems?.ToDictionary(x => x.NetworkName, x => x)
                           ?? new Dictionary<string, EvmChainGrainDto>();

        foreach (var op in _options.ContractConfig.Values)
        {
            try
            {
                var tempWeb3 = new Web3(op.Api);
                var latestBlockHeight = await _indexer.GetLatestBlockHeightAsync(tempWeb3);

                _logger.LogDebug($"[EvmGrain] Updated {op.NetworkName} index height to {latestBlockHeight}");

                currentState[op.NetworkName] = new EvmChainGrainDto
                {
                    NetworkName = op.NetworkName,
                    ConsumedBlockHeight = latestBlockHeight
                };
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"[EvmGrain] Updated {op.NetworkName} index height failed.");
            }
        }

        State.ChainItems = currentState.Values.ToList();

        await WriteStateAsync();
    }

    public async Task<GrainResultDto<List<EvmChainGrainDto>>> GetBlockHeightAsync()
        => new() { Success = true, Data = State.ChainItems };

    public async Task<GrainResultDto<List<EvmRampRequestGrainDto>>> SearchEvmRequestsAsync(string network, long to,
        long from)
    {
        var op = _options.ContractConfig.Values.FirstOrDefault(t => t.NetworkName == network);
        if (op == null || string.IsNullOrEmpty(op.Api) || string.IsNullOrEmpty(op.ContractAddress))
        {
            _logger.LogError($"[EvmGrain] Invalid network {network}");
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
                    $"[EvmGrain] {network} Error processing blocks {curFrom} to {currentTo}: {ex.Message}");
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
                    Type = CrossChainTransactionType.CrossChainSend,
                    StartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
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
                $"[EvmGrain] Failed to decode event to sendEvent {log.TransactionHash} at {log.BlockNumber}");

            return new();
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"[EvmGrain] Decode {log.TransactionHash} fail at {log.BlockNumber}.");
            throw;
        }
    }
}