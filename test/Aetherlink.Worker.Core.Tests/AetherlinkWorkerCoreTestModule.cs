using AetherLink.Worker.Core;
using Volo.Abp.Modularity;

namespace Aetherlink.Worker.Core.Tests;

[DependsOn(typeof(AetherlinkTestBaseModule),
    typeof(AetherLinkServerWorkerCoreModule))]
public class AetherlinkWorkerCoreTestModule:AbpModule
{
}