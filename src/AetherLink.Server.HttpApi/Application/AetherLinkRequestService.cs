using AetherLink.Server.Grains.Grain.Request;
using AetherLink.Server.HttpApi.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.ObjectMapping;
using AetherLink.Server.HttpApi.Constants;
using AetherLink.Server.HttpApi.Reporter;

namespace AetherLink.Server.HttpApi.Application;

public interface IAetherLinkRequestService
{
    Task<BasicResponseDto<GetCrossChainRequestStatusResponse>> GetCrossChainRequestStatusAsync(
        GetCrossChainRequestStatusInput input);
}

public class AetherLinkRequestService : AetherLinkServerAppService, IAetherLinkRequestService
{
    private readonly IObjectMapper _objectMapper;
    private readonly IClusterClient _clusterClient;
    private readonly CrossChainReporter _crossChainReporter;
    private readonly ILogger<AetherLinkRequestService> _logger;

    public AetherLinkRequestService(IClusterClient clusterClient, IObjectMapper objectMapper,
        ILogger<AetherLinkRequestService> logger, CrossChainReporter crossChainReporter)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _clusterClient = clusterClient;
        _crossChainReporter = crossChainReporter;
    }

    public async Task<BasicResponseDto<GetCrossChainRequestStatusResponse>> GetCrossChainRequestStatusAsync(
        GetCrossChainRequestStatusInput input)
    {
        var crossChainRequestGrainId = string.Empty;
        if (string.IsNullOrEmpty(input.TraceId) && string.IsNullOrEmpty(input.TransactionId))
        {
            throw new UserFriendlyException("Invalid parameter: transactionId and traceId cannot both be empty.");
        }

        _crossChainReporter.ReportCrossChainQueryTotalCount(input.TransactionId ?? input.TraceId);

        if (!string.IsNullOrEmpty(input.TraceId))
        {
            var traceGrain = _clusterClient.GetGrain<ITraceIdGrain>(input.TraceId);
            var traceResponse = await traceGrain.GetAsync();
            if (!traceResponse.Success)
            {
                _crossChainReporter.ReportCrossChainQueryHitCount(input.TraceId, MetricsConstants.ChainTon, false);
                throw new UserFriendlyException($"Not found traceId {input.TraceId}.");
            }

            crossChainRequestGrainId = traceResponse.Data.GrainId;
            _logger.LogDebug(
                $"[AetherLinkRequestService]Get CrossChainRequest status query by traceId {input.TraceId}");
        }
        else if (!string.IsNullOrEmpty(input.TransactionId))
        {
            if (input.TransactionId.StartsWith("0x"))
            {
                var tempGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(input.TransactionId);
                var tempResult = await tempGrain.GetAsync();
                if (tempResult is not { Success: true } || tempResult.Data == null)
                {
                    _crossChainReporter.ReportCrossChainQueryHitCount(input.TransactionId, MetricsConstants.ChainEvm,
                        false);
                    return new();
                }

                crossChainRequestGrainId = tempResult.Data.MessageId;
            }
            else
            {
                crossChainRequestGrainId = input.TransactionId;
            }

            _logger.LogDebug(
                $"[AetherLinkRequestService]Get CrossChainRequest status query by TransactionId {crossChainRequestGrainId}");
        }

        var orderGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(crossChainRequestGrainId);
        var result = await orderGrain.GetAsync();
        if (!result.Success || result.Data == null)
        {
            _logger.LogError($"[AetherLinkRequestService]GetAsync failed or returned null Data for grainId: {crossChainRequestGrainId}, Success: {result.Success}");
            if (!string.IsNullOrEmpty(input.TraceId))
                _crossChainReporter.ReportCrossChainQueryHitCount(input.TraceId, MetricsConstants.ChainTon, false);
            else if (!string.IsNullOrEmpty(input.TransactionId) && input.TransactionId.StartsWith("0x"))
                _crossChainReporter.ReportCrossChainQueryHitCount(input.TransactionId, MetricsConstants.ChainEvm,
                    false);
            else
                _crossChainReporter.ReportCrossChainQueryHitCount(input.TransactionId, MetricsConstants.ChainAelf,
                    false);
            throw new UserFriendlyException("Failed to get cross chain transaction");
        }

        _crossChainReporter.ReportCrossChainQueryHitCount(crossChainRequestGrainId,
            result.Data.SourceChainId.ToString(), true);
        var response = _objectMapper.Map<CrossChainRequestGrainDto, GetCrossChainRequestStatusResponse>(result.Data);
        return new() { Data = response };
    }
}