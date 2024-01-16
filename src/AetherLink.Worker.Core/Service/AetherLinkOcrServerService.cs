using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Utils;
using Microsoft.Extensions.Logging;

namespace AetherLink.Worker.Core.Service;

public class AetherLinkOcrServerService : AetherLinkService.AetherLinkServiceBase
{
    private readonly Dictionary<MessageType, IStreamMethod> _streamMethods;
    private readonly ILogger<AetherLinkOcrServerService> _logger;

    public AetherLinkOcrServerService(IEnumerable<IStreamMethod> streamMethods, ILogger<AetherLinkOcrServerService> logger)
    {
        _logger = logger;
        _streamMethods = streamMethods.ToDictionary(x => x.Method, y => y);
    }

    public override async Task<VoidReply> RequestStreamAsync(IAsyncStreamReader<StreamMessage> requestStream,
        ServerCallContext context)
    {
        try
        {
            await requestStream.ForEachAsync(async req =>
            {
                if (!_streamMethods.TryGetValue(req.MessageType, out var method)) return;

                await method.InvokeAsync(req);
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[gRPC][Server] Aether link Service RequestStream error.");
            // todo: add exception handling
        }

        return new VoidReply();
    }
}