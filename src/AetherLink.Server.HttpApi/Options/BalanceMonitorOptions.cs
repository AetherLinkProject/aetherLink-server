namespace AetherLink.Server.HttpApi.Options;

using System.Collections.Generic;

public class BalanceMonitorOptions
{
    public int Period { get; set; } = 10000;
    public Dictionary<string, ChainBalanceMonitorOptions> Chains { get; set; } = new();
}

public class ChainBalanceMonitorOptions
{
    public List<string> Addresses { get; set; } = new();
    public string Url { get; set; }
    public string ApiKey { get; set; }
    public string ChainType { get; set; }
} 