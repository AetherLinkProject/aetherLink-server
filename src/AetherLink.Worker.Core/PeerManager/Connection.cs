using System;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using AetherLink.Worker.Core.Consts;

namespace AetherLink.Worker.Core.PeerManager;

public class Connection
{
    private readonly AetherLinkServer.AetherLinkServerClient _client;

    public Connection(string endpoint)
    {
        // todo: complete ip address validate 
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentNullException(nameof(endpoint));

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

        // without protocol
        var channel = endpoint.Split(":").Length < 3
            ? options.Credentials == ChannelCredentials.Insecure
                ? GrpcChannel.ForAddress(new Uri($"{NetworkConstants.InsecurePrefix}{endpoint}"), options)
                : GrpcChannel.ForAddress(new Uri($"{NetworkConstants.SecurePrefix}{endpoint}"), options)
            : GrpcChannel.ForAddress(endpoint, options);

        _client = new AetherLinkServer.AetherLinkServerClient(channel);
    }

    public TResponse CallAsync<TResponse>(Func<AetherLinkServer.AetherLinkServerClient, TResponse> callFunc) =>
        callFunc.Invoke(_client);
}