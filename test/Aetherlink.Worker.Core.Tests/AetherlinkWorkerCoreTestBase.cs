using Volo.Abp.Modularity;

namespace Aetherlink.Worker.Core.Tests;

public class AetherlinkWorkerCoreTestBase<TStartupModule> : AetherlinkTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
}