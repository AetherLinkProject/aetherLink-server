using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace AetherLink.MockServer.Services;

[RemoteService]
public class GoogleController : AbpControllerBase
{
    [HttpGet]
    [Route("/oauth2/v3/certs")]
    public async Task<GoogleKidResponseDto> GetGoogleKidAsync()
        => new() { Keys = new() { new() { N = "n-1", Kid = "kid-1" }, new() { N = "n-2", Kid = "kid-2" } } };
}

public class GoogleKidResponseDto
{
    public List<GoogleKidDto> Keys { get; set; }
}

public class GoogleKidDto
{
    public string N { get; set; }
    public string Kid { get; set; }
    public string E { get; set; } = "AQAB";
    public string Alg { get; set; } = "RS256";
    public string Kty { get; set; } = "RSA";
    public string Use { get; set; } = "sig";
}