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
