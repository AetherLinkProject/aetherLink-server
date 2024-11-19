using AetherLink.Server.HttpApi.Dtos;
using Orleans;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Server.HttpApi.Application;

public interface IAetherLinkRequestService
{
    Task<BasicResponseDto<GetCrossChainRequestStatusResponse>> GetCrossChainRequestStatusAsync(
        GetCrossChainRequestStatusInput input);
}

public class AetherLinkRequestService : IAetherLinkRequestService, ISingletonDependency
{
    private readonly IClusterClient _clusterClient;

    public AetherLinkRequestService(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public async Task<BasicResponseDto<GetCrossChainRequestStatusResponse>> GetCrossChainRequestStatusAsync(
        GetCrossChainRequestStatusInput input)
    {
        return new();
    }
}