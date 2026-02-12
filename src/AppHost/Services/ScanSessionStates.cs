namespace AppHost.Services;

public static class ScanSessionStates
{
    public const string Queued = "Queued";
    public const string Counting = "Counting";
    public const string Running = "Running";
    public const string Paused = "Paused";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Stopped = "Stopped";

    public static bool IsTerminal(string state)
    {
        return state is Completed or Failed or Stopped;
    }
}
