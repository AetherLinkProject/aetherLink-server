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

public class EvmIndexerOptions
{
    public string WsUrl { get; set; } = "wss://sepolia.infura.io/ws/v3/a22808b9b0f14e9dbb098f2b03604ce2";
    public string ContractAddress { get; set; } = "0xf9Ab39c7A0A925BAf94f9C1c1d1CE8bFc9F9b2b3";
}