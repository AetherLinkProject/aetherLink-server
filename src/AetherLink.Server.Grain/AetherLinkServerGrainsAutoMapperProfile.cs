using AetherLink.Server.Grains.Grain.Request;
using AetherLink.Server.Grains.State;
using AutoMapper;

namespace AetherLink.Server.Grains;

public class AetherLinkServerGrainsAutoMapperProfile : Profile
{
    public AetherLinkServerGrainsAutoMapperProfile()
    {
        CreateMap<CrossChainRequestGrainDto, CrossChainRequestState>();
        CreateMap<CrossChainRequestState, CrossChainRequestGrainDto>();
    }
}