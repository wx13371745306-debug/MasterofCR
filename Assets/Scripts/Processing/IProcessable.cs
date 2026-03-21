public interface IProcessable
{
    ProcessType SupportedProcessType { get; }

    float CurrentProgress { get; }
    float RequiredProgress { get; }
    float NormalizedProgress { get; }
    bool IsComplete { get; }

    bool CanProcess(ProcessType processType);
    void ApplyProgress(ProcessType processType, float amount, BaseStation sourceStation);
}