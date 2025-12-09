namespace AetherLink.Worker.Core.JobPipeline.Args;

public class VrfTxResultCheckJobArgs : VRFJobArgs
{
    public string TransmitTransactionId { get; set; }
}