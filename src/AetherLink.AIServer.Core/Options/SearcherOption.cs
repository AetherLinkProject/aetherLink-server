namespace AetherLink.AIServer.Core.Options;

public class SearcherOption
{
    public string ChainId { get; set; } = "AELF";
    public int Timer { get; set; } = 3000;
    public long StartHeight { get; set; } = -1;
}