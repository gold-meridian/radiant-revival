using System;

namespace RadiantRevival.Common;

public readonly ref struct ScopeStateCapture<T> : IDisposable
{
    private readonly T oldValue;
    private readonly ref T reference;
    
    public ScopeStateCapture(ref T value)
    {
        oldValue = value;
        reference = ref value;
    }

    public void Dispose()
    {
        reference = oldValue;
    }
}

