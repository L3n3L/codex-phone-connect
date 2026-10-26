namespace CodexPhoneConnect;

public sealed record SendJobRequest(
    string Text,
    string? RequestId,
    string? Token,
    string? AgentId = null);

public sealed record ScrollRequest(
    int DeltaY,
    string? RequestId,
    string? Token,
    string? AgentId = null);

public sealed record AgentSelectionRequest(string? AgentId);

public sealed record SendJobAccepted(string JobId, string Status);

public sealed record ScrollCommandAccepted(string CommandId, string Status);

public sealed record AgentSelectionAccepted(string AgentId, string Status);

public sealed record BridgeStatus(
    string State,
    bool Ready,
    string Address,
    string ActiveAgent,
    IReadOnlyList<string> Addresses);

public abstract record BridgeInputCommand(string CommandId, AgentId Agent);

public sealed record BridgeJob(string JobId, string Text, AgentId Agent)
    : BridgeInputCommand(JobId, Agent);

public sealed record BridgeScrollCommand(string CommandId, int DeltaY, AgentId Agent)
    : BridgeInputCommand(CommandId, Agent);

public sealed class SendJobReceivedEventArgs(string jobId, SendJobRequest request, AgentId agent) : EventArgs
{
    public string JobId { get; } = jobId;

    public SendJobRequest Request { get; } = request;

    public AgentId Agent { get; } = agent;
}

public sealed class ScrollCommandReceivedEventArgs(string commandId, ScrollRequest request, AgentId agent) : EventArgs
{
    public string CommandId { get; } = commandId;

    public ScrollRequest Request { get; } = request;

    public AgentId Agent { get; } = agent;
}

public sealed class ActiveAgentChangedEventArgs(AgentId agent) : EventArgs
{
    public AgentId Agent { get; } = agent;
}
