namespace AetherlinkPriceServer.Worker.Dtos;

public class HamsterPriceResponseDto
{
    public string Code { get; set; }
    public HamsterPriceDataDto Data { get; set; }
}

public class HamsterPriceDataDto
{
    public double AcornsInElf { get; set; }
    public decimal AcornsInUsd { get; set; }
}