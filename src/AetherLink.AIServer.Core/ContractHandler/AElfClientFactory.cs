using System.Collections.Concurrent;
using AElf.Client.Service;
using AetherLink.AIServer.Core.Options;
using Microsoft.Extensions.Options;

namespace AetherLink.AIServer.Core.ContractHandler;

public class AElfClientFactory : IBlockchainClientFactory<AElfClient>
{
    private readonly ContractOptions _options;
    private readonly ConcurrentDictionary<string, AElfClient> _clientDic;

    public AElfClientFactory(IOptionsSnapshot<ContractOptions> chainOptions)
    {
        _options = chainOptions.Value;
        _clientDic = new ConcurrentDictionary<string, AElfClient>();
    }

    public AElfClient GetClient(string chainName)
    {
        var chainInfo = _options.ChainInfos[chainName];
        if (_clientDic.TryGetValue(chainName, out var client))
        {
            return client;
        }

        client = new AElfClient(chainInfo.BaseUrl);
        _clientDic[chainName] = client;
        return client;
    }
}