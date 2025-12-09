namespace Aetherlink.PriceServer.Common;

public static class TokenPairHelper
{
    public static bool IsValidTokenPair(string tokenPair)
        => !string.IsNullOrEmpty(tokenPair) && tokenPair.Contains('-') && tokenPair.IndexOf("-") > 0 &&
           tokenPair.LastIndexOf("-") < tokenPair.Length - 1;
}