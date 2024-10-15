using System;
using System.Threading;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Constants;
using AetherLink.Worker.Core.JobPipeline.Args;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.PeerManager;

public interface IConnection
{
    Task Init(string domain);

    TResponse CallAsync<TResponse>(Func<AetherLinkServer.AetherLinkServerClient, TResponse> callFunc);

    Task<bool> IsConnectionReady();
}

public class Connection : IConnection, ITransientDependency
{
    private GrpcChannel _channel;

    private AetherLinkServer.AetherLinkServerClient _client;

    public async Task Init(string endpoint)
    {
        // todo: complete ip address validate 

        var options = new GrpcChannelOptions
        {
            Credentials = ChannelCredentials.Insecure,
            InitialReconnectBackoff = TimeSpan.FromSeconds(GrpcConstants.DefaultInitialBackoff),
            MaxReconnectBackoff = TimeSpan.FromSeconds(GrpcConstants.DefaultMaxBackoff),
            ServiceConfig = new ServiceConfig
            {
                MethodConfigs =
                {
                    // https://learn.microsoft.com/en-us/aspnet/core/grpc/retries
                    new MethodConfig
                    {
                        // This method is configured with MethodName.Default, so it's applied to all gRPC methods called by this channel.
                        Names = { MethodName.Default },
                        RetryPolicy = new RetryPolicy
                        {
                            MaxAttempts = GrpcConstants.DefaultMaxAttempts,
                            InitialBackoff = TimeSpan.FromSeconds(GrpcConstants.DefaultInitialBackoff),
                            MaxBackoff = TimeSpan.FromSeconds(GrpcConstants.DefaultMaxBackoff),
                            BackoffMultiplier = GrpcConstants.DefaultBackoffMultiplier,
                            RetryableStatusCodes = { StatusCode.Unavailable }
                        }
                    }
                }
            }
        };

        // todo: handle with localhost

        // without protocol
        _channel = endpoint.Split(":").Length < 3
            ? options.Credentials == ChannelCredentials.Insecure
                ? GrpcChannel.ForAddress(new Uri($"{NetworkConstants.InsecurePrefix}{endpoint}"), options)
                : GrpcChannel.ForAddress(new Uri($"{NetworkConstants.SecurePrefix}{endpoint}"), options)
            : GrpcChannel.ForAddress(endpoint, options);

        _client = new AetherLinkServer.AetherLinkServerClient(_channel);
    }

    public TResponse CallAsync<TResponse>(Func<AetherLinkServer.AetherLinkServerClient, TResponse> callFunc) =>
        callFunc.Invoke(_client);

    [ExceptionHandler(typeof(OperationCanceledException), TargetType = typeof(CommonExceptionHanding),
        MethodName = nameof(CommonExceptionHanding.RethrowException))]
    [ExceptionHandler(typeof(Exception), TargetType = typeof(Connection), MethodName = nameof(HandleException))]
    public virtual async Task<bool> IsConnectionReady()
    {
        // Because grpc update connection state is locked, there is no need to add additional locks to obtain the state here, and then establish the connection.
        switch (_channel.State)
        {
            case ConnectivityState.Ready:
                return true;
            case ConnectivityState.Idle:
            case ConnectivityState.Connecting:
            case ConnectivityState.TransientFailure:
                var context =
                    new CancellationTokenSource(TimeSpan.FromSeconds(GrpcConstants.DefaultConnectTimeout));
                _channel.ConnectAsync(context.Token);
                return true;
            case ConnectivityState.Shutdown:
            default:
                return false;
        }
    }

    #region Exception Handing

    public async Task<FlowBehavior> HandleException(Exception ex)
    {
        return new FlowBehavior()
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }

    #endregion
}