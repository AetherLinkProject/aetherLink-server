using System.Collections.Generic;
using Aetherlink.PriceServer.Dtos;

namespace AetherlinkPriceServer.Options;

public class TokenPriceSourceOptions
{
    public Dictionary<string, TokenPriceSourceOption> Sources { get; set; }

    public TokenPriceSourceOption GetSourceOption(SourceType sourceType)
        => Sources.GetValueOrDefault(sourceType.ToString());

    public List<string> GetTokenList(SourceType sourceType) =>
        Sources.GetValueOrDefault(sourceType.ToString()).Tokens ?? new List<string> { "ELF" };
}

public class TokenPriceSourceOption
{
    public string Name { get; set; }
    public string ApiKey { get; set; }
    public string BaseUrl { get; set; }
    public int Interval { get; set; } = 3000;
    public List<string> Tokens { get; set; }
}