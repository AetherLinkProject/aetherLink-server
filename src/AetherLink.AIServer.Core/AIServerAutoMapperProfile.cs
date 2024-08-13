using AetherLink.AIServer.Core.Dtos;
using AutoMapper;

namespace AetherLink.AIServer.Core;

public class AetherLinkAIServerAutoMapperProfile : Profile
{
    public AetherLinkAIServerAutoMapperProfile()
    {
        CreateMap<AIRequestDto, RequestStorageDto>();
    }
}