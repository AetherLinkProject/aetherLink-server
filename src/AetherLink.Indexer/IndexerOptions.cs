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
    public string ContractAddress { get; set; } = "0xdaEe625927C292BB4E29b800ABeCe0Dadf10EbAb";
}

public class BscIndexerOptions
{
    public string WsUrl { get; set; } = "wss://go.getblock.io/31c578f1bfa74beb9e9d1c9e0068f059";
    public string ContractAddress { get; set; } = "0xdaEe625927C292BB4E29b800ABeCe0Dadf10EbAb";
}