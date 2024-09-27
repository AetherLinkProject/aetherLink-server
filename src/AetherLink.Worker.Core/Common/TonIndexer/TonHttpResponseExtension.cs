using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AetherLink.Worker.Core.Common.TonIndexer;

public static class TonHttpResponseExtension
{
    public static async Task<T> DeserializeSnakeCaseHttpContent<T>(this HttpContent content)
    {
        var respData = await content.ReadAsStringAsync();
        var serializeSetting = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            }
        };

        return JsonConvert.DeserializeObject<T>(respData, serializeSetting);
    }
}