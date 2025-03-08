using AetherLink.Server.Grains.Grain.Request;
using AetherLink.Server.HttpApi.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.ObjectMapping;

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
    private readonly ILogger<AetherLinkRequestService> _logger;

    public AetherLinkRequestService(IClusterClient clusterClient, IObjectMapper objectMapper,
        ILogger<AetherLinkRequestService> logger)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _clusterClient = clusterClient;
    }

    public async Task<BasicResponseDto<GetCrossChainRequestStatusResponse>> GetCrossChainRequestStatusAsync(
        GetCrossChainRequestStatusInput input)
    {
        string crossChainRequestGrainId;
        if (!string.IsNullOrEmpty(input.TraceId))
        {
            var traceGrain = _clusterClient.GetGrain<ITraceIdGrain>(input.TraceId);
            var traceResponse = await traceGrain.GetAsync();
            if (!traceResponse.Success)
            {
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
                if (!tempResult.Success) throw new UserFriendlyException("Failed to get cross chain transaction");
                crossChainRequestGrainId = tempResult.Data.MessageId;
            }
            else crossChainRequestGrainId = input.TransactionId;

            _logger.LogDebug(
                $"[AetherLinkRequestService]Get CrossChainRequest status query by TransactionId {input.TransactionId}");
        }
        else
        {
            throw new UserFriendlyException("Not found.");
        }

        var orderGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(crossChainRequestGrainId);
        var result = await orderGrain.GetAsync();
        if (!result.Success) throw new UserFriendlyException("Failed to get cross chain transaction");
        var response = _objectMapper.Map<CrossChainRequestGrainDto, GetCrossChainRequestStatusResponse>(result.Data);
        return new() { Data = response };
    }
}