using System.Collections.Generic;

namespace AetherlinkPriceServer.Options;

public class DailyPriceOption
{
    public bool SwitchOn { get; set; }
    public List<string> Tokens { get; set; } = new() { "elf-usd", "elf-usdt" };
}