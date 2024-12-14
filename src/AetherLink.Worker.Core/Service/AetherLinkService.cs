using System.Threading.Tasks;
using Grpc.Core;

namespace AetherLink.Worker.Core.Service;

public class AetherLinkService : AetherLinkServer.AetherLinkServerBase
{
    private readonly IServiceProcessor _requestProcessor;

    public AetherLinkService(IServiceProcessor requestProcessor)
    {
        _requestProcessor = requestProcessor;
    }

    public override Task<VoidReply> QueryObservationAsync(QueryObservationRequest request, ServerCallContext context)
    {
        _requestProcessor.ProcessQueryAsync(request, context);
        return Task.FromResult(new VoidReply());
    }

    public override Task<VoidReply> CommitObservationAsync(CommitObservationRequest request, ServerCallContext context)
    {
        _requestProcessor.ProcessObservationAsync(request, context);
        return Task.FromResult(new VoidReply());
    }

    public override Task<VoidReply> CommitReportAsync(CommitReportRequest request, ServerCallContext context)
    {
        _requestProcessor.ProcessReportAsync(request, context);
        return Task.FromResult(new VoidReply());
    }

    public override Task<VoidReply> CommitSignatureAsync(CommitSignatureRequest request, ServerCallContext context)
    {
        _requestProcessor.ProcessProcessedReportAsync(request, context);
        return Task.FromResult(new VoidReply());
    }

    public override Task<VoidReply> CommitTransmitResultAsync(CommitTransmitResultRequest request,
        ServerCallContext context)
    {
        _requestProcessor.ProcessTransmittedResultAsync(request, context);
        return Task.FromResult(new VoidReply());
    }

    public override Task<VoidReply> QueryReportSignatureAsync(QueryReportSignatureRequest request,
        ServerCallContext context)
    {
        _requestProcessor.ProcessReportSignatureAsync(request, context);
        return Task.FromResult(new VoidReply());
    }

    public override Task<VoidReply> CommitPartialSignatureAsync(CommitPartialSignatureRequest request,
        ServerCallContext context)
    {
        _requestProcessor.ProcessPartialSignatureAsync(request, context);
        return Task.FromResult(new VoidReply());
    }

    public override Task<VoidReply> BroadcastTransmitResultAsync(BroadcastTransmitResult request,
        ServerCallContext context)
    {
        _requestProcessor.ProcessTransmitResultAsync(request, context);
        return Task.FromResult(new VoidReply());
    }

    public override Task<VoidReply> QueryMessageSignatureAsync(QueryMessageSignatureRequest request,
        ServerCallContext context)
    {
        _requestProcessor.ProcessMessagePartialSignatureQueryAsync(request, context);
        return Task.FromResult(new VoidReply());
    }

    public override Task<VoidReply> ReturnPartialSignatureResultsAsync(ReturnPartialSignatureResults request,
        ServerCallContext context)
    {
        _requestProcessor.ProcessMessagePartialSignatureReturnAsync(request, context);
        return Task.FromResult(new VoidReply());
    }

    public override Task<VoidReply> CrossChainReceivedResultCheckAsync(CrossChainReceivedResult request,
        ServerCallContext context)
    {
        _requestProcessor.ProcessCrossChainReceivedResultAsync(request, context);
        return Task.FromResult(new VoidReply());
    }
}