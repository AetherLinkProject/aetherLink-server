using Aetherlink.PriceServer.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace AetherLink.MockServer.Services;

[RemoteService]
public class PriceController : AbpControllerBase
{
    [HttpGet]
    [Route("/api/v1/aggregatedPrice")]
    public async Task<AggregatedPriceResponseDto> GetPriceAsync(GetAggregatedTokenPriceRequestDto input) => new()
        { Data = new() { Price = 60000000 } };
}