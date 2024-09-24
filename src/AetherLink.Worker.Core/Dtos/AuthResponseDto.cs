using System.Collections.Generic;

namespace AetherLink.Worker.Core.Dtos;

public class AuthResponseDto
{
    public List<AuthKeyDto> Keys { get; set; }
}

public class AuthKeyDto
{
    public string Kty { get; set; }
    public string Kid { get; set; }
    public string Use { get; set; }
    public string Alg { get; set; }
    public string N { get; set; }
    public string E { get; set; }
}