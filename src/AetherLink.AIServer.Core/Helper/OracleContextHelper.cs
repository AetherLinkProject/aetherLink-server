using AElf;
using AElf.Types;
using AetherLink.Contracts.AIFeeds;

namespace AetherLink.AIServer.Core.Helper;

public static class OracleContextHelper
{
    public static OracleContext GenerateOracleContextAsync(string chainId, string requestId) =>
        new() { ChainId = ChainHelper.ConvertBase58ToChainId(chainId), RequestId = Hash.LoadFromHex(requestId) };
}