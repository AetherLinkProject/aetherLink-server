using System.IO;
using System.Threading.Tasks;
using AElf;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.Options;
using Microsoft.Extensions.Options;

namespace AetherLink.Worker.Core.OCR;

public class Ocr
{
    // Extract: Extract report from rpc bytes
    public void Extract()
    {
    }

    // Accept: Accept report after extract report
    public void Accept()
    {
    }


    // 直接进行签名
    public MultiSignature Process(MultiSignature sign, PartialSignatureDto partialSign, string secret)
    {
        // var id = IdGeneratorHelper.GenerateMultiSignatureId(args);
        // var sign = _stateProvider.GetMultiSignature(id);
        // if (sign == null)
        // {
        //     var newMultiSignature = new MultiSignature(ByteArrayHelper.HexStringToByteArray(secret),
        //         msg, config.DistPublicKey, config.PartialSignaturesThreshold);
        //     newMultiSignature.GeneratePartialSignature();
        //     _stateProvider.SetMultiSignature(id, newMultiSignature);
        //     _reporter.RecordMultiSignatureAsync(args.ChainId, args.RequestId, args.Epoch);
        //     return;
        // }
        //
        // if (!sign.ProcessPartialSignature(args.PartialSignature))
        // {
        //     _reporter.RecordMultiSignatureProcessResultAsync(args.ChainId, args.RequestId, args.Epoch,
        //         args.PartialSignature.Index, "failed");
        // }

        // _stateProvider.SetMultiSignature(id, sign);
        // _reporter.RecordMultiSignatureAsync(args.ChainId, args.RequestId, args.Epoch);
        return sign;
    }

    public async Task TransmitAsync()
    {
    }
}