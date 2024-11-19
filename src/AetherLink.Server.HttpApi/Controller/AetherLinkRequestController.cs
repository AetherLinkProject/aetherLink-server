using System.Threading.Tasks;
using AetherLink.Server.HttpApi.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace AetherLink.Server.HttpApi.Controller;

[RemoteService]
[Route("api/v1")]
public class AetherLinkRequestController : AetherLinkServerController
{
    [HttpGet]
    [Route("status/oracle")]
    public async Task<string> GetOracleRequestStatusAsync() => "pong";

    [HttpGet]
    [Route("status/crossChain")]
    public async Task<BasicResponseDto<GetCrossChainRequestStatusResponse>> GetCrossChainRequestStatusAsync(
        GetCrossChainRequestStatusInput input) => new();
}