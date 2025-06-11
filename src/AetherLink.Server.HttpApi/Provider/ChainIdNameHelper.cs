namespace AetherLink.Server.HttpApi.Provider
{
    public static class ChainIdNameHelper
    {
        private static readonly Dictionary<long, string> ChainIdMap = new()
        {
            { 1100, "ton" },
            { 56, "bsc" },
            { 97, "bsc" },
            { 1, "evm" },
            { 8453, "base" },
            { 84532, "basesepolia" },
            { 11155111, "sepolia" },
            { 9992731, "aelf" },
            { 1866392, "tdvv" },
            { 1931928, "tdvw" }
        };

        public static string ToChainName(long chainId)
            => ChainIdMap.TryGetValue(chainId, out var name) ? name : chainId.ToString();
    }
}