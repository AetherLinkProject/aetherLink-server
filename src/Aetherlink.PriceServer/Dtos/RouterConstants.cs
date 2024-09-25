namespace Aetherlink.PriceServer.Dtos;

public static class RouterConstants
{
    public const string ROUTERGROUP = "/api/v1/";
    public const string TOKEN_PRICE_URI = ROUTERGROUP + "price";
    public const string TOKEN_PRICE_LIST_URI = ROUTERGROUP + "prices";
    public const string DAILY_PRICE_URI = ROUTERGROUP + "price/daily";
    public const string LAST_24HOURS_PRICE_URI = ROUTERGROUP + "prices/hours";
    public const string AGGREGATED_TOKEN_PRICE_URI = ROUTERGROUP + "aggregatedPrice";
}