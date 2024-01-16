using System.Collections.Generic;

namespace AetherLink.Worker.Core.Common;

public static class IdGeneratorHelper
{
    public static string GenerateId(params object[] ids) => ids.JoinAsString("-");
}