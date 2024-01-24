using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AetherLink.Multisignature;
using AetherLink.Worker.Core.Dtos;
using Volo.Abp.DependencyInjection;

namespace AetherLink.Worker.Core.Provider;

public interface IStateProvider
{
    public bool IsReportGenerated(string id);
    public void SetReportGeneratedFlag(string id);
    public List<ObservationDto> GetPartialObservation(string id);
    public void AddOrUpdateReport(string id, ObservationDto observation);
    public MultiSignature GetMultiSignature(string id);
    public void SetMultiSignature(string id, MultiSignature signature);
    public bool GetMultiSignatureSignedFlag(string id);
    public void SetMultiSignatureSignedFlag(string id);
    public long GetFollowerObservationCurrentEpoch(string id);
    public void SetFollowerObservationCurrentEpoch(string id, long epoch);
    public void CleanEpochState(string id);
}

public class StateProvider : IStateProvider, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, bool> _signedFlag = new();
    private readonly ConcurrentDictionary<string, bool> _reportGeneratedFlag = new();
    private readonly ConcurrentDictionary<string, List<ObservationDto>> _reports = new();
    private readonly ConcurrentDictionary<string, long> _observationCurrentEpochs = new();
    private readonly ConcurrentDictionary<string, MultiSignature> _multiSignatureDict = new();

    // ReportGenerated
    public bool IsReportGenerated(string id) => _reportGeneratedFlag.TryGetValue(id, out _);
    public void SetReportGeneratedFlag(string id) => _reportGeneratedFlag[id] = true;

    // PartialObservation
    public List<ObservationDto> GetPartialObservation(string id) =>
        _reports.TryGetValue(id, out var index) ? index : null;

    public void AddOrUpdateReport(string id, ObservationDto observation)
    {
        if (!_reports.TryGetValue(id, out var observations))
        {
            _reports[id] = new List<ObservationDto> { observation };
        }
        else if (observations.All(o => o.Index != observation.Index))
        {
            _reports[id].Add(observation);
        }
    }

    public long GetFollowerObservationCurrentEpoch(string id)
        => _observationCurrentEpochs.TryGetValue(id, out var currentEpoch) ? currentEpoch : 0;

    public void SetFollowerObservationCurrentEpoch(string id, long epoch) => _observationCurrentEpochs[id] = epoch;

    // MultiSignature
    public MultiSignature GetMultiSignature(string id)
        => _multiSignatureDict.TryGetValue(id, out var multiSignature) ? multiSignature : null;

    public void SetMultiSignature(string id, MultiSignature signature) => _multiSignatureDict[id] = signature;

    // MultiSignatureSignedFlag
    public bool GetMultiSignatureSignedFlag(string id) => _signedFlag.TryGetValue(id, out _);

    public void SetMultiSignatureSignedFlag(string id) => _signedFlag[id] = true;

    // Clean up transmitted KV
    public void CleanEpochState(string id)
    {
    }
}