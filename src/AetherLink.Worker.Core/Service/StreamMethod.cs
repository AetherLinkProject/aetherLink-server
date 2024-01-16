using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Service;

public interface IStreamMethod
{
    MessageType Method { get; }
    Task InvokeAsync(StreamMessage request);
}

public abstract class StreamMethod : IStreamMethod
{
    public abstract MessageType Method { get; }
    protected readonly IGrpcRequestProcessor GrpcRequestProcessor;

    protected StreamMethod(IGrpcRequestProcessor grpcRequestProcessor)
    {
        GrpcRequestProcessor = grpcRequestProcessor;
    }

    public abstract Task InvokeAsync(StreamMessage request);
}

public class RequestJobMethod : StreamMethod, ISingletonDependency
{
    public RequestJobMethod(IGrpcRequestProcessor grpcRequestProcessor) : base(grpcRequestProcessor)
    {
    }

    public override MessageType Method => MessageType.RequestJob;

    public override async Task InvokeAsync(StreamMessage request)
    {
        await GrpcRequestProcessor.RequestJobAsync(RequestJob.Parser.ParseFrom(request.Message));
    }
}

public class RequestDataMessageMethod : StreamMethod, ISingletonDependency
{
    public RequestDataMessageMethod(IGrpcRequestProcessor grpcRequestProcessor) : base(grpcRequestProcessor)
    {
    }

    public override MessageType Method => MessageType.RequestData;

    public override async Task InvokeAsync(StreamMessage request)
    {
        await GrpcRequestProcessor.RequestDataMessageAsync(DataMessage.Parser.ParseFrom(request.Message));
    }
}

public class RequestReportMethod : StreamMethod, ISingletonDependency
{
    public RequestReportMethod(IGrpcRequestProcessor grpcRequestProcessor) : base(grpcRequestProcessor)
    {
    }

    public override MessageType Method => MessageType.RequestReport;

    public override async Task InvokeAsync(StreamMessage request)
    {
        await GrpcRequestProcessor.RequestReportAsync(Observations.Parser.ParseFrom(request.Message));
    }
}

public class RequestReportSignatureMethod : StreamMethod, ISingletonDependency
{
    public RequestReportSignatureMethod(IGrpcRequestProcessor grpcRequestProcessor) : base(grpcRequestProcessor)
    {
    }

    public override MessageType Method => MessageType.RequestReportSignature;

    public override async Task InvokeAsync(StreamMessage request)
    {
        await GrpcRequestProcessor.RequestReportSignatureAsync(ReportSignature.Parser.ParseFrom(request.Message));
    }
}

public class RequestTransactionResultMethod : StreamMethod, ISingletonDependency
{
    public RequestTransactionResultMethod(IGrpcRequestProcessor grpcRequestProcessor) : base(grpcRequestProcessor)
    {
    }

    public override MessageType Method => MessageType.TransmittedResult;

    public override async Task InvokeAsync(StreamMessage request)
    {
        await GrpcRequestProcessor.RequestTransactionResulAsync(TransactionResult.Parser.ParseFrom(request.Message));
    }
}