using System.Collections.Concurrent;
using System.Collections.Generic;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.Dtos;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IStateProvider
{
    public bool GetReportGeneratedFlag(string key);
    public void SetReportGeneratedFlag(string key);
    public List<ObservationDto> GetPartialObservation(string key);
    public void SetPartialObservation(string key, List<ObservationDto> queue);
    public MultiSignature GetMultiSignature(string id);
    public void SetMultiSignature(string id, MultiSignature multisignature);
    public bool GetMultiSignatureSignedFlag(string key);
    public void SetMultiSignatureSignedFlag(string key);
    public void CleanEpochState(string key, long epoch);
}

public class StateProvider : IStateProvider, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, MultiSignature> _multiSignatureDict = new();
    private readonly ConcurrentDictionary<string, bool> _signedFlag = new();
    private readonly ConcurrentDictionary<string, bool> _reportGeneratedFlag = new();
    private readonly ConcurrentDictionary<string, List<ObservationDto>> _reports = new();

    // ReportGenerated
    public bool GetReportGeneratedFlag(string key)
    {
        return _reportGeneratedFlag.TryGetValue(key, out _);
    }

    public void SetReportGeneratedFlag(string key)
    {
        _reportGeneratedFlag[key] = true;
    }

    // PartialObservation
    public List<ObservationDto> GetPartialObservation(string key)
    {
        if (_reports.TryGetValue(key, out var index)) return index;
        return null;
    }

    public void SetPartialObservation(string key, List<ObservationDto> observations)
    {
        _reports[key] = observations;
    }

    // MultiSignature
    public MultiSignature GetMultiSignature(string id)
    {
        if (_multiSignatureDict.TryGetValue(id, out var multiSignature)) return multiSignature;
        return null;
    }

    public void SetMultiSignature(string id, MultiSignature multisignature)
    {
        _multiSignatureDict[id] = multisignature;
    }

    // MultiSignatureSignedFlag
    public bool GetMultiSignatureSignedFlag(string key)
    {
        return _signedFlag.TryGetValue(key, out _);
    }

    public void SetMultiSignatureSignedFlag(string key)
    {
        _signedFlag[key] = true;
    }

    // Clean up transmitted KV
    public void CleanEpochState(string key, long epoch)
    {
    }
}