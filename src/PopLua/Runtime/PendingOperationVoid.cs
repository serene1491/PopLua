namespace PopLua.Runtime;

internal sealed class PendingOperationVoid : PendingOperation
{
    private readonly ValueTask _task;

    internal PendingOperationVoid(ValueTask task, bool pauseActiveTime)
        : base(pauseActiveTime)
    {
        _task = task;

        if (!task.IsCompleted)
            return;

        try
        {
            task.GetAwaiter().GetResult();
            CompleteSuccess();
        }
        catch (Exception ex)
        {
            CompleteFailure(ex);
        }
    }

    internal override int PushResult(nint state) => 0;

    protected override async ValueTask WaitCoreAsync(CancellationToken cancellation)
        => await _task.AsTask().WaitAsync(cancellation).ConfigureAwait(false);
}
