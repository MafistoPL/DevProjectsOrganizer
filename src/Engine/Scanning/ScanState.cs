namespace Engine.Scanning;

public enum ScanState
{
    Queued,
    Counting,
    Scanning,
    Paused,
    Completed,
    Failed,
    Stopped
}
