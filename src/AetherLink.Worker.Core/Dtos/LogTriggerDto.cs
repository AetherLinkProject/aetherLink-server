using System;

namespace AetherLink.Worker.Core.Dtos;

public class LogTriggerDto : OCRBasicDto
{
    public string TransactionEventStorageId { get; set; }
    public string LogUpkeepStorageId { get; set; }
    public DateTime ReceiveTime { get; set; }
    public RequestState State { get; set; }
}