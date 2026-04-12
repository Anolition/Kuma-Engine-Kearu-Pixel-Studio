namespace ProjectSPlus.Core.Configuration;

public static class EditorAutosaveOptions
{
    public static readonly int[] SecondsSteps = [0, 2, 5, 10, 30, 60];

    public static int Normalize(int seconds)
    {
        if (seconds <= 0)
        {
            return 0;
        }

        int nearest = SecondsSteps[0];
        int nearestDelta = int.MaxValue;
        foreach (int candidate in SecondsSteps)
        {
            int delta = Math.Abs(candidate - seconds);
            if (delta < nearestDelta)
            {
                nearest = candidate;
                nearestDelta = delta;
            }
        }

        return nearest;
    }

    public static string ToLabel(int seconds)
    {
        int normalized = Normalize(seconds);
        return normalized <= 0 ? "Off" : $"{normalized}s";
    }
}
