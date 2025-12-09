using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;


namespace AetherLink.Worker.Core.Common;

public class GraphQLHelper
{
    public static async Task<T> SendQueryAsync<T>(IGraphQLClient client, GraphQLRequest request)
    {
        var graphQlResponse = await client.SendQueryAsync<T>(request);
        return graphQlResponse.Errors is not { Length: > 0 } ? graphQlResponse.Data : default;
    }
}