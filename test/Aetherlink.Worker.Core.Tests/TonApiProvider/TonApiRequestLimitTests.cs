using AetherLink.Worker.Core.Provider.TonIndexer;
using Shouldly;

namespace Aetherlink.Worker.Core.Tests.TonApiProvider;

public class TonApiRequestLimitTests:AetherlinkTestBase<AetherlinkWorkerCoreTestModule>
{
    [Fact]
    public async Task TonApi_Request_Not_Limit_Tests()
    {
        var requestLimit = new TonapiRequestLimit(1);
        
        var firstTest = requestLimit.TryGetAccess();
        firstTest.ShouldBeTrue();
    }

    [Fact]
    public async Task TonApi_Request_Limit_Tests()
    {
        // var requestLimit = new TonapiRequestLimit(1);
        // requestLimit.TryGetAccess().ShouldBeTrue();
        // requestLimit.TryGetAccess().ShouldBeFalse();
        //
        // await Task.Delay(1 * 1000);
        //
        // requestLimit.TryGetAccess().ShouldBeTrue();
    }
}