#if DEBUG
using System;
using System.IO;
using System.Text;

namespace GameInterface.Services.CampaignService;

internal interface ICampaignLoadPhaseDiagnostic
{
    void RecordStarted(string phase);
    void RecordCompleted(string phase);
}

internal class CampaignLoadPhaseDiagnostic : ICampaignLoadPhaseDiagnostic
{
    private const string BreadcrumbFileName = "Coop_campaign_load_phase.log";
    private static readonly object SyncRoot = new object();
    private static readonly Encoding Encoding = new UTF8Encoding(false);

    public void RecordStarted(string phase)
    {
        Write("started", phase);
    }

    public void RecordCompleted(string phase)
    {
        Write("completed", phase);
    }

    private static void Write(string state, string phase)
    {
        if (string.IsNullOrWhiteSpace(phase)) throw new ArgumentException("Phase is required", nameof(phase));

        string line = $"{state}|{phase}{Environment.NewLine}";
        lock (SyncRoot)
        {
            using (var stream = new FileStream(
                BreadcrumbFileName,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                4096,
                FileOptions.WriteThrough))
            {
                byte[] bytes = Encoding.GetBytes(line);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }
    }
}
#endif
