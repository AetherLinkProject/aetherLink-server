namespace AetherLink.Server.HttpApi.Options;

using System.Collections.Generic;

public class BalanceMonitorOptions
{
    public Dictionary<string, ChainBalanceMonitorOptions> Chains { get; set; } = new();
}

public class ChainBalanceMonitorOptions
{
    public List<string> Addresses { get; set; } = new();
} 