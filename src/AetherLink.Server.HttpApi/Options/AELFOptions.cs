namespace AetherLink.Server.HttpApi.Options;

public class AELFOptions
{
    public int ConfirmBlockHeightTimer { get; set; } = 2000;
    public int RequestSearchTimer { get; set; } = 3000;
    public int CommitSearchTimer { get; set; } = 8000;
}