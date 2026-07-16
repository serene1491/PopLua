namespace PopLua.Interop;

internal sealed class AllocatorTracker
{
    internal AllocatorTracker(nuint maxHeapBytes, nuint gcThresholdBytes)
    {
        MaxHeapBytes = maxHeapBytes;
        GcThresholdBytes = gcThresholdBytes;
    }

    internal nuint MaxHeapBytes { get; }
    internal nuint GcThresholdBytes { get; }
    internal nuint CurrentBytes { get; private set; }
    internal nuint PeakBytes { get; private set; }
    internal bool MemoryLimitExceeded { get; private set; }
    internal bool GcRequested { get; private set; }

    internal void ResetQuotaState()
    {
        MemoryLimitExceeded = false;
        GcRequested = false;
    }

    internal bool TryResize(nuint oldSize, nuint newSize)
    {
        var current = CurrentBytes;
        var next = current >= oldSize
            ? current - oldSize + newSize
            : newSize;

        if (MaxHeapBytes > 0 && next > MaxHeapBytes)
        {
            MemoryLimitExceeded = true;
            return false;
        }

        CurrentBytes = next;
        if (next > PeakBytes)
            PeakBytes = next;

        if (GcThresholdBytes > 0 && next >= GcThresholdBytes)
            GcRequested = true;

        return true;
    }

    internal void Free(nuint oldSize)
    {
        CurrentBytes = CurrentBytes >= oldSize ? CurrentBytes - oldSize : 0;
    }

    internal void RestoreAfterFailedResize(nuint oldSize, nuint newSize)
    {
        if (newSize > oldSize)
        {
            Free(newSize - oldSize);
        }
        else if (oldSize > newSize)
        {
            CurrentBytes += oldSize - newSize;
        }
    }

    internal bool ConsumeGcRequest()
    {
        if (!GcRequested)
            return false;

        GcRequested = false;
        return true;
    }
}
