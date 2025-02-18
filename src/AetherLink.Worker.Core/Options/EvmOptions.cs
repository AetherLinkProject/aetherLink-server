namespace AetherLink.Worker.Core.Options;

public class EvmOptions
{
    public string ContractAddress { get; set; }
    public string SkipTransactionLt { get; set; } = "0";
    public string Url { get; set; } = "https://sepolia.infura.io/v3/";
    public string ApiKey { get; set; } = "a22808b9b0f14e9dbb098f2b03604ce2";
}