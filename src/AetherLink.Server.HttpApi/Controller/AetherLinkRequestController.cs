using System.Threading.Tasks;
using AetherLink.Server.HttpApi.Application;
using AetherLink.Server.HttpApi.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace AetherLink.Server.HttpApi.Controller;

[RemoteService]
[Route("api/v1")]
public class AetherLinkRequestController : AetherLinkServerController
{
    private readonly IAetherLinkRequestService _requestService;

    public AetherLinkRequestController(IAetherLinkRequestService requestService)
    {
        _requestService = requestService;
    }

    [HttpGet]
    [Route("status/oracle")]
    public async Task<string> GetOracleRequestStatusAsync() => "pong";

    [HttpGet]
    [Route("status/crossChain")]
    public async Task<BasicResponseDto<GetCrossChainRequestStatusResponse>> GetCrossChainRequestStatusAsync(
        GetCrossChainRequestStatusInput input) => await _requestService.GetCrossChainRequestStatusAsync(input);
}