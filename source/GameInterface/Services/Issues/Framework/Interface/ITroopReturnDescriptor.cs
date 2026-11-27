using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Interface;

/// <summary>A quest descriptor whose server troop return replaces vanilla's <c>IssueManager.TryToMakeTroopsReturn</c>.</summary>
public interface ITroopReturnDescriptor
{
    // Returns the sent troops, true when vanilla's return must not run
    bool ReturnTroops(IssueBase issue);
}
