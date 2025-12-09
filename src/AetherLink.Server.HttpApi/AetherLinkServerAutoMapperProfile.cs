using AetherLink.Server.Grains.Grain.Request;
using AetherLink.Server.HttpApi.Dtos;
using AutoMapper;

namespace AetherLink.Server.HttpApi;

public class AetherLinkServerAutoMapperProfile : Profile
{
    public AetherLinkServerAutoMapperProfile()
    {
        CreateMap<CrossChainRequestGrainDto, GetCrossChainRequestStatusResponse>().ReverseMap();
    }
}