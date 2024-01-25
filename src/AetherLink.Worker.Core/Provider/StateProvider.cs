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

    // public void AddOrUpdateReport(string id, ObservationDto observation);
    public MultiSignature GetMultiSignature(string id);
    public void SetMultiSignature(string id, MultiSignature signature);
    public long GetObserveCurrentEpoch(string id);
    public void SetObserveCurrentEpoch(string id, long epoch);
    public void CleanEpochState(string id);
    public bool IsFinished(string id);
    public void SetFinishedFlag(string id);
}

public class StateProvider : IStateProvider, ISingletonDependency
{
    // private readonly ConcurrentDictionary<string, bool> _signedFlag = new();
    // private readonly ConcurrentDictionary<string, bool> _reportGeneratedFlag = new();
    private readonly ConcurrentDictionary<string, bool> _finishedFlag = new();
    private readonly ConcurrentDictionary<string, long> _observeEpochs = new();
    private readonly ConcurrentDictionary<string, List<ObservationDto>> _reports = new();
    private readonly ConcurrentDictionary<string, MultiSignature> _multiSignatures = new();

    // Report
    public List<ObservationDto> GetObservations(string id) => _reports.TryGetValue(id, out var list) ? list : null;
    public void SetObservations(string id, List<ObservationDto> observations) => _reports[id] = observations;

    // public void AddOrUpdateReport(string id, ObservationDto observation)
    // {
    //     if (!_reports.TryGetValue(id, out var observations))
    //     {
    //         _reports[id] = new List<ObservationDto> { observation };
    //     }
    //     else if (observations.All(o => o.Index != observation.Index))
    //     {
    //         _reports[id].Add(observation);
    //     }
    // }

    // observation collect current epoch
    public long GetObserveCurrentEpoch(string id) => _observeEpochs.TryGetValue(id, out var current) ? current : 0;
    public void SetObserveCurrentEpoch(string id, long epoch) => _observeEpochs[id] = epoch;

    // MultiSignature
    public MultiSignature GetMultiSignature(string id) => _multiSignatures.TryGetValue(id, out var sign) ? sign : null;
    public void SetMultiSignature(string id, MultiSignature signature) => _multiSignatures[id] = signature;

    // Generate finished flag
    public bool IsFinished(string id) => _finishedFlag.TryGetValue(id, out _);
    public void SetFinishedFlag(string id) => _finishedFlag[id] = true;

    // Clean up transmitted KV
    public void CleanEpochState(string id)
    {
    }
}