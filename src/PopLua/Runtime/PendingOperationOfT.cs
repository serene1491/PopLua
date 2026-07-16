namespace PopLua.Runtime;

internal sealed class PendingOperation<T> : PendingOperation
{
    private readonly ValueTask<T> _task;
    private readonly Func<nint, T, int> _pusher;
    private T? _result;

    internal PendingOperation(
        ValueTask<T> task,
        Func<nint, T, int> pusher,
        bool pauseActiveTime)
        : base(pauseActiveTime)
    {
        _task = task;
        _pusher = pusher;

        if (!task.IsCompleted)
            return;

        try
        {
            _result = task.GetAwaiter().GetResult();
            CompleteSuccess();
        }
        catch (Exception ex)
        {
            CompleteFailure(ex);
        }
    }

    internal override int PushResult(nint state)
    {
        if (!IsSuccess)
            throw new ScriptException(ErrorMessage(this));

        return _pusher(state, _result!);
    }

    protected override async ValueTask WaitCoreAsync(CancellationToken cancellation)
        => _result = await _task.AsTask().WaitAsync(cancellation).ConfigureAwait(false);
}
