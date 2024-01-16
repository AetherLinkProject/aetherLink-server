using AetherLink.Worker.Core.Consts;

namespace AetherLink.Worker.Core.Dtos;

public class DataFeedsDto
{
    public string Cron { get; set; }
    public DataFeedsJobSpec DataFeedsJobSpec { get; set; }
}

public class DataFeedsJobSpec
{
    public DataFeedsType Type { get; set; }
    public string CurrencyPair { get; set; }
}

public class PriceDataFeedsJobSpec : DataFeedsJobSpec
{
    public string CurrencyPair { get; set; }
}