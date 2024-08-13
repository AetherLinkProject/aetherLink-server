using System.Collections.Generic;

namespace AetherLink.AIServer.Core.Options;

public class ContractOptions
{
    public Dictionary<string, ChainInfo> ChainInfos { get; set; }
}

public class ChainInfo
{
    public string BaseUrl { get; set; }
}