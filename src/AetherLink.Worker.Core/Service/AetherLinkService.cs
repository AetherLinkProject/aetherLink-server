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
}