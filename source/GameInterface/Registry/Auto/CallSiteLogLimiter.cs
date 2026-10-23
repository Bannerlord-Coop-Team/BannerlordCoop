using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace GameInterface.Registry.Auto;

/// <summary>
/// Keeps a noisy diagnostic useful without flooding the log. The first hit from each call site is
/// reported in full, later hits from a known site are only counted and handed back as a periodic total.
/// </summary>
internal sealed class CallSiteLogLimiter
{
    private readonly object gate = new();
    private readonly HashSet<string> knownSites = new();
    private readonly int maxSites;
    private readonly TimeSpan summaryInterval;
    private readonly Func<DateTime> utcNow;
    private DateTime lastSummary;
    private int suppressed;

    public CallSiteLogLimiter(int maxSites, TimeSpan summaryInterval, Func<DateTime> utcNow = null)
    {
        this.maxSites = maxSites;
        this.summaryInterval = summaryInterval;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        lastSummary = this.utcNow();
    }

    /// <summary>
    /// Returns true the first time a call site is seen, so the caller logs it in full.
    /// Otherwise the hit is counted and <paramref name="suppressedCount"/> is set once per summary
    /// interval to the number of hits that were not logged since the last summary.
    /// </summary>
    public bool ShouldLogFull(string site, out int suppressedCount)
    {
        suppressedCount = 0;
        lock (gate)
        {
            if (knownSites.Count < maxSites && knownSites.Add(site)) return true;

            suppressed++;
            var now = utcNow();
            if (now - lastSummary < summaryInterval) return false;

            suppressedCount = suppressed;
            suppressed = 0;
            lastSummary = now;
            return false;
        }
    }

    /// <summary>
    /// Logs <paramref name="fullTemplate"/> with the stack trace appended for a new call site,
    /// otherwise <paramref name="summaryTemplate"/> with the suppressed count appended.
    /// </summary>
    // call it straight from the patch, the site key skips this method and its caller
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Log(ILogger logger, string fullTemplate, object[] fullArgs, string summaryTemplate, params object[] summaryArgs)
    {
        var site = GetCallSite(2, 6);
        if (ShouldLogFull(site, out int suppressedCount))
        {
            logger.Error(fullTemplate, Append(fullArgs, Environment.StackTrace));
        }
        else if (suppressedCount > 0)
        {
            logger.Warning(summaryTemplate, Append(summaryArgs, suppressedCount));
        }
    }

    private static object[] Append(object[] args, object last)
    {
        var all = new object[args.Length + 1];
        args.CopyTo(all, 0);
        all[args.Length] = last;
        return all;
    }

    /// <summary>
    /// Short signature of the current call stack built from method names only, which is much cheaper
    /// than <see cref="Environment.StackTrace"/> because it skips file and line lookup.
    /// </summary>
    public static string GetCallSite(int skipFrames, int frameCount)
    {
        var frames = new StackTrace(skipFrames + 1, false).GetFrames();
        var site = new StringBuilder();
        for (int i = 0; i < frames.Length && i < frameCount; i++)
        {
            var method = frames[i].GetMethod();
            site.Append(method?.DeclaringType?.FullName).Append('.').Append(method?.Name).Append('|');
        }
        return site.ToString();
    }
}
