using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Options;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Network;

public interface INetworkServer
{
    Task StartAsync();
}

public class NetworkServer : INetworkServer, ISingletonDependency
{
    private Server _server;
    private readonly AetherLinkService.AetherLinkServiceBase _serverService;
    private readonly NetworkOptions _options;

    public NetworkServer(AetherLinkService.AetherLinkServiceBase serverService,
        IOptionsSnapshot<NetworkOptions> options)
    {
        _serverService = serverService;
        _options = options.Value;
    }

    public Task StartAsync()
    {
        _server = new Server();
        _server.Services.Add(AetherLinkService.BindService(_serverService));
        _server.Ports.Add(new ServerPort(IPAddress.Any.ToString(), _options.ListenPort, ServerCredentials.Insecure));

        return Task.Run(() => { _server.Start(); });
    }
}