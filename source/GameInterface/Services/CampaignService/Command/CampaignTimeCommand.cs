using System;
using System.Collections.Generic;
using System.Text;
using static TaleWorlds.Library.CommandLineFunctionality;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CampaignService.Command;
public class CampaignTimeCommand
{

    // coop.debug.clan.list
    /// <summary>
    /// Lists all the clans
    /// </summary>
    /// <param name="args">actually none are being used..</param>
    /// <returns>strings of all the clans</returns>
    [CommandLineArgumentFunction("time", "coop.debug.campaign")]
    public static string ListTime(List<string> args)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine(CampaignTime.Now.ToString());
        sb.AppendLine("---------------------");
        sb.AppendLine("Campaign.Current.MpaTimeTracker");
        sb.AppendLine(Campaign.CurrentTime.ToString());
        sb.AppendLine("---------------------------");
        sb.AppendLine("Values Synced:");
        sb.AppendLine($"\tnumTicks = {Campaign.Current.MapTimeTracker._numTicks}");
        sb.AppendLine($"\tdeltaTime = {Campaign.Current.MapTimeTracker._deltaTimeInTicks}");



        return sb.ToString();
    }

}
