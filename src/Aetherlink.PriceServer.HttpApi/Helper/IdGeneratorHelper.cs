using System.Collections.Generic;

namespace AetherlinkPriceServer.Helper;

public static class IdGeneratorHelper
{
    public static string GenerateId(params object[] ids) => ids.JoinAsString("-");
}