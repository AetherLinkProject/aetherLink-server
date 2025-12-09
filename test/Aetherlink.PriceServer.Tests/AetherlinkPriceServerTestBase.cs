namespace Aetherlink.PriceServer.Tests;

public class AetherlinkPriceServerTestBase : AetherlinkTestBase<AetherlinkPriceServerHttpApiTestModule>
{
    protected const string ELFUSDT = "elf-usdt";
    protected const string BTCUSDT = "btc-usdt";
    protected const long ELFPRICE = 60000000;
    protected const long BTCPRICE = 7000000000000;
}