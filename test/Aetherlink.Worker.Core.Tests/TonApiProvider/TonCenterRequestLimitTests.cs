using AetherLink.Worker.Core.Provider.TonIndexer;
using Shouldly;

namespace Aetherlink.Worker.Core.Tests.TonApiProvider;

public class TonCenterRequestLimitTests:AetherlinkTestBase<AetherlinkWorkerCoreTestModule>
{
    [Fact]
    public async Task TonCenter_Request_Not_Limit_Tests()
    {
        var requestLimit = new TonCenterRequestLimit(1);
        
        var firstTest = requestLimit.TryGetAccess();
        firstTest.ShouldBeTrue();
    }

    [Fact]
    public async Task TonCenter_Request_Limit_Tests()
    {
        var requestLimit = new TonCenterRequestLimit(1);
        requestLimit.TryGetAccess().ShouldBeTrue();
        requestLimit.TryGetAccess().ShouldBeFalse();
        
        await Task.Delay(1 * 1000);
        
        requestLimit.TryGetAccess().ShouldBeTrue();
    }
}