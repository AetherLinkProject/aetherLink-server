using GraphQL.Types;

namespace AetherLink.MockServer.GraphQL;

public class AetherlinkIndexerSchema : Schema
{
    public AetherlinkIndexerSchema(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        Query = new AutoRegisteringObjectGraphType<Query>();
    }
}