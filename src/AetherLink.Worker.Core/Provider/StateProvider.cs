using System.Collections.Concurrent;
using System.Collections.Generic;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.Dtos;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IStateProvider
{
    public List<ObservationDto> GetObservations(string id);
    public void SetObservations(string id, List<ObservationDto> observations);
    public MultiSignature GetMultiSignature(string id);
    public Dictionary<int, byte[]> GetTonMultiSignature(string id);
    public void SetTonMultiSignature(string id, Dictionary<int, byte[]> signatures);
    public void SetMultiSignature(string id, MultiSignature signature);
    public bool IsFinished(string id);
    public void SetFinishedFlag(string id);
}

public class StateProvider : IStateProvider, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, bool> _finishedFlag = new();
    private readonly ConcurrentDictionary<string, List<ObservationDto>> _reports = new();
    private readonly ConcurrentDictionary<string, MultiSignature> _multiSignatures = new();
    private readonly ConcurrentDictionary<string, Dictionary<int, byte[]>> _tonMultiSignatures = new();

    // Report
    public List<ObservationDto> GetObservations(string id) => _reports.TryGetValue(id, out var list) ? list : null;
    public void SetObservations(string id, List<ObservationDto> observations) => _reports[id] = observations;

    // MultiSignature
    public MultiSignature GetMultiSignature(string id) => _multiSignatures.TryGetValue(id, out var sign) ? sign : null;
    public void SetMultiSignature(string id, MultiSignature signature) => _multiSignatures[id] = signature;

    // Generate finished flag
    public bool IsFinished(string id) => _finishedFlag.TryGetValue(id, out _);
    public void SetFinishedFlag(string id) => _finishedFlag[id] = true;

    // TON Multi Signature
    public Dictionary<int, byte[]> GetTonMultiSignature(string id) =>
        _tonMultiSignatures.TryGetValue(id, out var sign) ? sign : null;

    public void SetTonMultiSignature(string id, Dictionary<int, byte[]> signatures) =>
        _tonMultiSignatures[id] = signatures;
}