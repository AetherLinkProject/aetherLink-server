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

public class EvmContractsOptions
{
    public Dictionary<string, EvmOptions> ContractConfig { get; set; } = new();
}

public class EvmOptions
{
    public string ContractAddress { get; set; }
    public string Api { get; set; }
    public string NetworkName { get; set; }
    public int SubscribeBlocksDelay { get; set; } = 100;
}