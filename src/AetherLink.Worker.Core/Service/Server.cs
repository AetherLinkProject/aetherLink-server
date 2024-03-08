using System.Net;
using System.Threading.Tasks;
using AetherLink.Worker.Core.Options;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Service;

public interface IServer
{
    Task StartAsync();
}

public class Server : IServer, ISingletonDependency
{
    private Grpc.Core.Server _server;
    private readonly NetworkOptions _options;
    private readonly AetherLinkServer.AetherLinkServerBase _serverService;

    public Server(AetherLinkServer.AetherLinkServerBase serverService, IOptionsSnapshot<NetworkOptions> options)
    {
        _options = options.Value;
        _serverService = serverService;
    }

    public Task StartAsync()
    {
        _server = new Grpc.Core.Server();
        _server.Services.Add(AetherLinkServer.BindService(_serverService));
        _server.Ports.Add(new ServerPort(IPAddress.Any.ToString(), _options.ListenPort, ServerCredentials.Insecure));

        return Task.Run(() =>
        {
            _server.Start();

            // _server.ShutdownAsync().Wait(TimeSpan.FromSeconds(GrpcConstants.GracefulShutdown));
            // _server.KillAsync();
        });
    }
}