namespace PopLua.Runtime;

internal sealed class FailedPendingOperation : PendingOperation
{
    internal FailedPendingOperation(Exception error)
        : base(pauseActiveTime: true)
        => CompleteFailure(error);

    internal override int PushResult(nint state)
        => throw new ScriptException(ErrorMessage(this));

    protected override ValueTask WaitCoreAsync(CancellationToken cancellation)
        => ValueTask.CompletedTask;
}
