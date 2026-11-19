namespace TatumWelcome2.BLL.Services;

/// <summary>How long an account is locked after repeated wrong PINs.</summary>
public static class PinLockoutPolicy
{
    public const int AttemptsPerStep = 5;
    public const int ResetAfterHours = 24;

    // 5 wrong PINs: 1 minute, 10: 5 minutes, 15: 15 minutes, 20 or more: 60 minutes.
    private static readonly int[] StepSeconds = { 60, 300, 900, 3600 };

    /// <summary>Lock length in seconds for this many failures in a row, or null for no lock.</summary>
    public static int? LockSeconds(int failedAttempts)
    {
        if (failedAttempts <= 0 || failedAttempts % AttemptsPerStep != 0)
            return null;

        int step = failedAttempts / AttemptsPerStep - 1;
        return StepSeconds[Math.Min(step, StepSeconds.Length - 1)];
    }
}