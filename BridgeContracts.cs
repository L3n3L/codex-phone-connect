namespace CodexPhoneConnect;

public sealed record SendJobRequest(string Text, string? RequestId, string? Token);

public sealed record ScrollRequest(int DeltaY, string? RequestId, string? Token);

public sealed record SendJobAccepted(string JobId, string Status);

public sealed record ScrollCommandAccepted(string CommandId, string Status);

public sealed record BridgeStatus(string State, bool Ready, string Address);

public abstract record BridgeInputCommand(string CommandId);

public sealed record BridgeJob(string JobId, string Text) : BridgeInputCommand(JobId);

public sealed record BridgeScrollCommand(string CommandId, int DeltaY) : BridgeInputCommand(CommandId);

public sealed class SendJobReceivedEventArgs(string jobId, SendJobRequest request) : EventArgs
{
    public string JobId { get; } = jobId;
    public SendJobRequest Request { get; } = request;
}

public sealed class ScrollCommandReceivedEventArgs(string commandId, ScrollRequest request) : EventArgs
{
    public string CommandId { get; } = commandId;

    public ScrollRequest Request { get; } = request;
}
