using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class TonBalanceProvider : IChainBalanceProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _tonApiUrl;

    public TonBalanceProvider(HttpClient httpClient, string tonApiUrl)
    {
        _httpClient = httpClient;
        _tonApiUrl = tonApiUrl;
    }

    public async Task<decimal> GetBalanceAsync(string address)
    {
        var response = await _httpClient.GetAsync(_tonApiUrl + address);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var json = JObject.Parse(content);
        var balanceStr = json["result"]?.ToString() ?? "0";
        decimal.TryParse(balanceStr, out var balance);
        return balance;
    }
} 