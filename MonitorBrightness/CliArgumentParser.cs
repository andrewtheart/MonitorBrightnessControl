namespace MonitorBrightness;

internal static class CliArgumentParser
{
    public static bool IsHelpFlag(string arg)
    {
        return arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
               arg.Equals("/help", StringComparison.OrdinalIgnoreCase) ||
               arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
               arg.Equals("-?", StringComparison.OrdinalIgnoreCase) ||
               arg.Equals("/?", StringComparison.OrdinalIgnoreCase);
    }

    public static List<MonitorDevice> ResolveTargetMonitors(IReadOnlyList<MonitorDevice> monitors, IEnumerable<string> targetTokens)
    {
        var targetIndexes = new SortedSet<int>();

        foreach (var rawToken in targetTokens)
        {
            foreach (var token in rawToken.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var monitor in monitors.Where(monitor => monitor.SupportsBrightness))
                        targetIndexes.Add(monitor.Index + 1);
                    continue;
                }

                if (TryParseRange(token, out int start, out int end))
                {
                    int min = Math.Min(start, end);
                    int max = Math.Max(start, end);
                    foreach (var monitor in monitors)
                    {
                        int displayIndex = monitor.Index + 1;
                        if (displayIndex >= min && displayIndex <= max)
                            targetIndexes.Add(displayIndex);
                    }
                    continue;
                }

                if (int.TryParse(token, out int index))
                    targetIndexes.Add(index);
            }
        }

        return targetIndexes
            .Select(index => monitors.FirstOrDefault(monitor => monitor.Index + 1 == index))
            .Where(monitor => monitor is not null)
            .Cast<MonitorDevice>()
            .ToList();
    }

    /// <summary>
    /// Collects all values following <paramref name="flagName"/> until the next
    /// <c>--</c> flag or end of args.
    /// </summary>
    public static string[] ExtractValues(string[] args, string flagName)
    {
        var values = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals(flagName, StringComparison.OrdinalIgnoreCase))
            {
                for (int j = i + 1; j < args.Length; j++)
                {
                    if (args[j].StartsWith("--", StringComparison.Ordinal))
                        break;
                    values.Add(args[j]);
                }
                break;
            }
        }

        return values.ToArray();
    }

    /// <summary>
    /// Returns the single value immediately after <paramref name="flagName"/>,
    /// or <c>null</c> if the flag is absent or has no value.
    /// </summary>
    public static string? ExtractSingleValue(string[] args, string flagName)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(flagName, StringComparison.OrdinalIgnoreCase))
            {
                var next = args[i + 1];
                return next.StartsWith("--", StringComparison.Ordinal) ? null : next;
            }
        }
        return null;
    }

    private static bool TryParseRange(string token, out int start, out int end)
    {
        start = 0;
        end = 0;

        if (!token.Contains('-', StringComparison.Ordinal))
            return false;

        var rangeParts = token.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return rangeParts.Length == 2 &&
               int.TryParse(rangeParts[0], out start) &&
               int.TryParse(rangeParts[1], out end);
    }
}
