using AetherLink.Server.Grains.Grain.Request;
using AetherLink.Server.HttpApi.Dtos;
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

    public AetherLinkRequestService(IClusterClient clusterClient, IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
        _clusterClient = clusterClient;
    }

    public async Task<BasicResponseDto<GetCrossChainRequestStatusResponse>> GetCrossChainRequestStatusAsync(
        GetCrossChainRequestStatusInput input)
    {
        var orderGrain = _clusterClient.GetGrain<ICrossChainRequestGrain>(input.MessageId);
        var result = await orderGrain.GetAsync();
        if (!result.Success) throw new UserFriendlyException("Failed to get cross chain transaction");
        var response = _objectMapper.Map<CrossChainRequestGrainDto, GetCrossChainRequestStatusResponse>(result.Data);
        return new() { Data = response };
    }
}