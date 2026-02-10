namespace AppHost.Services;

public sealed class AsyncManualResetEvent
{
    private volatile TaskCompletionSource<bool> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AsyncManualResetEvent(bool initialState = true)
    {
        if (initialState)
        {
            _tcs.TrySetResult(true);
        }
    }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        return _tcs.Task.WaitAsync(cancellationToken);
    }

    public void Set()
    {
        _tcs.TrySetResult(true);
    }

    public void Reset()
    {
        if (_tcs.Task.IsCompleted)
        {
            _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
