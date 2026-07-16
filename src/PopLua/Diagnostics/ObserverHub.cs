using System.Diagnostics;

namespace PopLua.Diagnostics;

internal sealed class ObserverHub(IReadOnlyList<IRunObserver> observers)
{
    internal bool Any => observers.Count > 0;

    internal void Started(ScriptContext ctx, Step step)
    {
        if (observers.Count > 0)
            Notify(observer => observer.Started(ctx, step));
    }

    internal void Completed(ScriptContext ctx, Step step, in Metrics metrics)
    {
        foreach (var observer in observers)
        {
            try
            {
                observer.Completed(ctx, step, in metrics);
            }
            catch (Exception error)
            {
                Trace.TraceError("PopLua observer failed: {0}", error);
            }
        }
    }

    internal void Failed(ScriptContext ctx, Step step, RuntimeException error)
    {
        if (observers.Count > 0)
            Notify(observer => observer.Failed(ctx, step, error));
    }

    internal void Quota(ScriptContext ctx, Step step, QuotaKind kind)
    {
        if (observers.Count > 0)
            Notify(observer => observer.Quota(ctx, step, kind));
    }

    internal void Denied(ScriptContext ctx, Step step, string cap)
    {
        if (observers.Count > 0)
            Notify(observer => observer.Denied(ctx, step, cap));
    }

    internal void Loading(ScriptContext ctx, Load load)
    {
        if (observers.Count > 0)
            Notify(observer => observer.Loading(ctx, load));
    }

    internal void Loaded(ScriptContext ctx, Load load, bool cached)
    {
        if (observers.Count > 0)
            Notify(observer => observer.Loaded(ctx, load, cached));
    }

    internal void LoadFailed(ScriptContext ctx, Load load, RuntimeException error)
    {
        if (observers.Count > 0)
            Notify(observer => observer.LoadFailed(ctx, load, error));
    }

    private void Notify(Action<IRunObserver> notify)
    {
        foreach (var observer in observers)
        {
            try
            {
                notify(observer);
            }
            catch (Exception error)
            {
                Trace.TraceError("PopLua observer failed: {0}", error);
            }
        }
    }
}
