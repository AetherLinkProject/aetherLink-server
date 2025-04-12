namespace AetherLink.Worker.Core.Options;

public class TonCenterProviderApiConfig
{
    public string Url { get; set; }
    public string ApiKey { get; set; }
    public int TransactionsSubscribeDelay { get; set; } = 50;
}