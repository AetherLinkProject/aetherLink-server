using AetherLink.Server.Grains.Grain.Request;
using AutoMapper;

namespace AetherLink.Server.Grains;

public class AetherLinkServerGrainsAutoMapperProfile : Profile
{
    public AetherLinkServerGrainsAutoMapperProfile()
    {
        CreateMap<CrossChainRequestState, CrossChainRequestGrainDto>().ReverseMap();
        CreateMap<CrossChainRequestGrainDto, CrossChainRequestState>().ReverseMap();
    }
}