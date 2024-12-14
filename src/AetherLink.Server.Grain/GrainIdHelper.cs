namespace AetherLink.Server.Grains;

public static class GrainIdHelper
{
    public static string GenerateGrainId(params object[] ids) => ids.JoinAsString("-");
}