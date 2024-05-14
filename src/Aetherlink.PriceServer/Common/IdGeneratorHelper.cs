using System.Collections.Generic;

namespace Aetherlink.PriceServer.Common;

public static class IdGeneratorHelper
{
    public static string GenerateId(params object[] ids) => ids.JoinAsString("-");
}