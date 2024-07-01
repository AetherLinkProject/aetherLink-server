using System.Threading.Tasks;
using AElf;
using AElf.Types;
using AetherLink.Contracts.Oracle;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.Common;
using AetherLink.Worker.Core.Options;
using AetherLink.Worker.Core.Provider;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Automation;

public interface ISignatureProvider
{
    public Hash GenerateMsg(TransmitInput input);
    public void LeaderInitMultiSign(OCRContext context, byte[] report);
    public bool ProcessMultiSignAsync(OCRContext context, int index, byte[] signature);
    public Task<PartialSignatureDto> GeneratePartialSignAsync(OCRContext context, ByteString result);
}

public class SignatureProvider : ISignatureProvider, ISingletonDependency
{
    private readonly object _lock = new();
    private readonly OracleInfoOptions _options;
    private readonly IStateProvider _stateProvider;
    private readonly ILogger<SignatureProvider> _logger;
    private readonly IOracleContractProvider _oracleProvider;

    public SignatureProvider(IStateProvider stateProvider, IOptionsSnapshot<OracleInfoOptions> options,
        ILogger<SignatureProvider> logger,
        IOracleContractProvider oracleProvider)
    {
        _logger = logger;
        _options = options.Value;
        _oracleProvider = oracleProvider;
        _stateProvider = stateProvider;
    }

    public void LeaderInitMultiSign(OCRContext context, byte[] report)
    {
        lock (_lock)
        {
            var id = IdGeneratorHelper.GenerateMultiSignatureId(context.ChainId, context.RequestId, context.Epoch,
                context.RoundId);
            var sign = _stateProvider.GetMultiSignature(id);

            if (sign != null)
            {
                _logger.LogWarning("{id}'s signature is initialed", id);
                return;
            }

            var newMultiSignature = InitMultiSignature(context.ChainId, report);
            newMultiSignature.GeneratePartialSignature();
            _stateProvider.SetMultiSignature(id, newMultiSignature);

            _logger.LogDebug("{id}'s signature init successful", id);
        }
    }

    public bool ProcessMultiSignAsync(OCRContext context, int index, byte[] signature)
    {
        lock (_lock)
        {
            var id = IdGeneratorHelper.GenerateMultiSignatureId(context.ChainId, context.RequestId, context.Epoch,
                context.RoundId);
            var sign = _stateProvider.GetMultiSignature(id);

            if (sign == null) return false;
            if (!sign.ProcessPartialSignature(new() { Index = index, Signature = signature })) return false;

            _stateProvider.SetMultiSignature(id, sign);

            return true;
        }
    }

    public async Task<PartialSignatureDto> GeneratePartialSignAsync(OCRContext context, ByteString result)
        => InitMultiSignature(context.ChainId, GenerateMsg(await _oracleProvider.GenerateTransmitDataAsync(
            context.ChainId, context.RequestId, context.Epoch, result)).ToByteArray()).GeneratePartialSignature();

    public Hash GenerateMsg(TransmitInput input) => HashHelper.ConcatAndCompute(
        HashHelper.ComputeFrom(input.Report.ToByteArray()), HashHelper.ComputeFrom(input.ReportContext.ToString()));

    private MultiSignature InitMultiSignature(string chainId, byte[] msg)
    {
        if (_options.ChainConfig.TryGetValue(chainId, out var config))
            return new(ByteArrayHelper.HexStringToByteArray(config.SignerSecret), msg,
                config.DistPublicKey, config.PartialSignaturesThreshold);

        _logger.LogError("Not support chain {c}.", chainId);
        return null;
    }
}