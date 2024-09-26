using System;
using System.Runtime.InteropServices.JavaScript;
using AetherLink.Worker.Core.JobPipeline.Args;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.X509.Qualified;

namespace AetherLink.Worker.Core.Dtos;

public class TonChainTaskDto
{
    public TonChainTaskType Type { get; set; }
    
    public string Content { get; set; }

    public TonChainTaskDto(ResendTonBaseArgs resendDto)
    {
        Type = TonChainTaskType.Resend;
        Content = JsonConvert.SerializeObject(resendDto);
    }

    public TonChainTaskDto()
    {
        Type = TonChainTaskType.None;
    }

    public T Convert<T>()
    {
        if (typeof(T) == typeof(ResendTonBaseArgs) && Type == TonChainTaskType.Resend)
        {
            var result = JsonConvert.DeserializeObject<T>(Content);
            return result;
        }

        if (typeof(T) == typeof(ResendTonBaseArgs) && Type == TonChainTaskType.Receive)
        {
            var result = JsonConvert.DeserializeObject<T>(Content);
            return result;
        }

        throw new Exception($"TonChainTaskDto not support type '{nameof(T)}'");
    }
}

public enum TonChainTaskType
{
    None = 0,
    Resend = 1,
    Receive = 2,
}