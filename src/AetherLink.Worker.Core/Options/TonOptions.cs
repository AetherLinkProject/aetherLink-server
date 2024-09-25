namespace AetherLink.Worker.Core.Options;

public class TonPublicConfigOptions
{
    public string BaseUrl { get; set; }
    
    public string ApiKey { get; set; }
    
    public string ContractAddress { get; set; }
}

public class TonSecretConfigOptions
{
    public string TransmitterSecretKey { get; set; }

    public string TransmitterPublicKey { get; set; }
    
    public string TransmitterFee { get; set; }
}