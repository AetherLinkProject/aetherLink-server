using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Options;
using AetherLink.Server.HttpApi.Options;
using AetherLink.Server.HttpApi.Constants;

public class TonBalanceProvider : ChainBalanceProvider
{
    public override string ChainType => ChainTypesConstants.Ton;

    protected readonly HttpClient HttpClient;
    protected readonly string ChainName;
    protected readonly IOptionsSnapshot<BalanceMonitorOptions> Options;

    public TonBalanceProvider(HttpClient httpClient, IOptionsSnapshot<BalanceMonitorOptions> options)
    {
        Options = options;
        HttpClient = httpClient;
        ChainName = ChainNamesConstants.Ton;
    }

    protected string Url => Options.Value.Chains[ChainName].Url;
    protected string ApiKey => Options.Value.Chains[ChainName].ApiKey;

    public override async Task<decimal> GetBalanceAsync(string address)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{Url}?address={address}");
        if (!string.IsNullOrEmpty(ApiKey))
            request.Headers.Add("X-Api-Key", ApiKey);
        var response = await HttpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);
        var balanceStr = json["result"]?.ToString() ?? "0";
        decimal.TryParse(balanceStr, out var balance);
        return balance;
    }
}