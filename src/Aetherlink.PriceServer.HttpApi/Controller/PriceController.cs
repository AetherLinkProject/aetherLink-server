using System.Threading.Tasks;
using Aetherlink.PriceServer.Dtos;
using AetherlinkPriceServer.Application;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace AetherlinkPriceServer.Controller;

[RemoteService]
[Route("api/app")]
public class PriceController : AetherlinkPriceServerController
{
    private readonly IPriceAppService _priceAppService;

    public PriceController(IPriceAppService priceAppService)
    {
        _priceAppService = priceAppService;
    }

    [HttpGet]
    [Route("price")]
    public async Task<PriceResponseDto> GetTokenPriceAsync(GetTokenPriceRequestDto input)
        => await _priceAppService.GetTokenPriceAsync(input);

    [HttpGet]
    [Route("aggregatedPrice")]
    public async Task<AggregatedPriceResponseDto> GetAggregatedTokenPriceAsync(GetAggregatedTokenPriceRequestDto input)
        => await _priceAppService.GetAggregatedTokenPriceAsync(input);

    [HttpGet]
    [Route("prices")]
    public async Task<PriceListResponseDto> GetTokenPriceListAsync(GetTokenPriceListRequestDto input)
        => await _priceAppService.GetTokenPriceListAsync(input);
}