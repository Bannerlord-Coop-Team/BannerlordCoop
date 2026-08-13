#if DEBUG
using System;
using System.Globalization;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties;

internal interface IMobilePartyLoadStateDiagnostic
{
    void RecordStarted(MobileParty mobileParty);
    void RecordCompleted(MobileParty mobileParty);
}

internal class MobilePartyLoadStateDiagnostic : IMobilePartyLoadStateDiagnostic
{
    private const string BreadcrumbFileName = "Coop_mobile_party_load_state.log";
    private static readonly object SyncRoot = new object();
    private static readonly Encoding Encoding = new UTF8Encoding(false);

    public void RecordStarted(MobileParty mobileParty)
    {
        Write("started", mobileParty);
    }

    public void RecordCompleted(MobileParty mobileParty)
    {
        Write("completed", mobileParty);
    }

    private static void Write(string phase, MobileParty mobileParty)
    {
        if (mobileParty == null) throw new ArgumentNullException(nameof(mobileParty));

        CampaignVec2 position = mobileParty.Position;
        CampaignVec2 moveTargetPoint = mobileParty.MoveTargetPoint;
        string line = string.Format(
            CultureInfo.InvariantCulture,
            "{0}|{1}|navigation={2}|moveMode={3}|position={4:R},{5:R},{6}|target={7:R},{8:R},{9}|moveTargetParty={10}{11}",
            phase,
            mobileParty.StringId,
            mobileParty.DesiredAiNavigationType,
            mobileParty.PartyMoveMode,
            position.X,
            position.Y,
            position.IsOnLand,
            moveTargetPoint.X,
            moveTargetPoint.Y,
            moveTargetPoint.IsOnLand,
            mobileParty.MoveTargetParty?.StringId ?? "null",
            Environment.NewLine);

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
