namespace _40K.Core;

public static class Stats
{
    public static int Percentile(int[] data, double p)
    {
        if (data is null || data.Length == 0) { throw new ArgumentException("Empty data", nameof(data)); }
        if (p <= 0) { return data.Min(); }
        if (p >= 1) { return data.Max(); }

        var sorted = data.OrderBy(x => x).ToArray();
        var idx = (sorted.Length - 1) * p;
        var lo = (int)Math.Floor(idx);
        var hi = (int)Math.Ceiling(idx);
        if (lo == hi) { return sorted[lo]; }
        var frac = idx - lo;
        return (int)Math.Round(sorted[lo] + (sorted[hi] - sorted[lo]) * frac);
    }
}