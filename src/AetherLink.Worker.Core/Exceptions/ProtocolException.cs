using System;

namespace AetherLink.Worker.Core.Exceptions;

public class ProtocolException:Exception
{
    public ProtocolException(string message) : base(message)
    {
        
    }
}