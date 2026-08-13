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
        string temporaryFileName = BreadcrumbFileName + ".tmp";
        lock (SyncRoot)
        {
            using (var stream = new FileStream(
                temporaryFileName,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                byte[] bytes = Encoding.GetBytes(line);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (File.Exists(BreadcrumbFileName))
            {
                File.Replace(temporaryFileName, BreadcrumbFileName, null);
            }
            else
            {
                File.Move(temporaryFileName, BreadcrumbFileName);
            }
        }
    }
}
#endif
