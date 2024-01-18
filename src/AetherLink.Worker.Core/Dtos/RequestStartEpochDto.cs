namespace AetherLink.Worker.Core.Dtos;

public class RequestStartEpochRecord
{
    public RequestStartEpochDto RequestStartEpoch { get; set; }
}

public class RequestStartEpochDto
{
    public long Epoch { get; set; }
}