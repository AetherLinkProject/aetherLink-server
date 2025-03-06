using System.Collections.Generic;

namespace AetherLink.Indexer;

public class AeFinderOptions
{
    public string BaseUrl { get; set; }
    public string GraphQlUri { get; set; } = "/api/app/graphql/aetherlink";
    public string SyncStateUri { get; set; } = "/api/apps/sync-state/aetherlink";
}

public class TonIndexerOption
{
    public string ContractAddress { get; set; } = "EQBCOuvczf29HIGNxrJdsmTKIabHQ1j4dW2ojlYkcru3IOYy";
    public string Url { get; set; } = "https://testnet.toncenter.com";
    public string LatestTransactionLt { get; set; } = "28227653000001";
}

public class EvmIndexerOptionsMap
{
    public Dictionary<string, EvmIndexerOptions> ChainInfos { get; set; }
}

public class EvmIndexerOptions
{
    public string WsUrl { get; set; }
    public string ContractAddress { get; set; }
    public string NetworkName { get; set; }
    public int PingDelay { get; set; } = 5000;
}