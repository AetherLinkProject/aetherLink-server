using System;
using System.Threading;
using Aetherlink.PriceServer.Dtos;

namespace Aetherlink.PriceServer.Common;

public static class ContextHelper
{
    public static CancellationToken GeneratorCtx(int timeout = NetworkConstants.DefaultTimout) =>
        new CancellationTokenSource(TimeSpan.FromSeconds(timeout)).Token;
}