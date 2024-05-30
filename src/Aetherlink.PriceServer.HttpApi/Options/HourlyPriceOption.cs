using System.Collections.Generic;

namespace AetherlinkPriceServer.Options;

public class HourlyPriceOption
{
    public List<string> Tokens { get; set; } = new() { "elf-usdt", "btc-usdt", "eth-usdt" };
}