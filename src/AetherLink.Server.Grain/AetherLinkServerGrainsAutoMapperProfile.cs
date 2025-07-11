using AetherLink.Indexer.Dtos;
using AetherLink.Server.Grains.Grain.Indexer;
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
        CreateMap<TonTransactionDto, TonTransactionGrainDto>()
            .ForMember(dest => dest.Now, opt => opt.MapFrom(src => src.Now));
        CreateMap<TonInMessageDto, TonInMessageGrainDto>();
        CreateMap<TonMessageContentDto, TonMessageContentGrainDto>();
        CreateMap<VrfJobGrainDto, VrfJobState>();
        CreateMap<VrfJobState, VrfJobGrainDto>();
    }
}