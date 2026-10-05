namespace CodexPhoneConnect;

public sealed record SendJobRequest(string Text, string? RequestId, string? Token);

public sealed record SendJobAccepted(string JobId, string Status);

public sealed record BridgeStatus(string State, bool Ready, string Address);

public sealed class SendJobReceivedEventArgs(string jobId, SendJobRequest request) : EventArgs
{
    public string JobId { get; } = jobId;
    public SendJobRequest Request { get; } = request;
}
