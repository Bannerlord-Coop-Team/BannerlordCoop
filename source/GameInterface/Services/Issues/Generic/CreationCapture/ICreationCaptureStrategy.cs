namespace GameInterface.Services.Issues.Generic.CreationCapture;

public interface ICreationCaptureStrategy<TIssue, TFields>
{
    bool TryCaptureFields(TIssue issue, out TFields fields);

    TIssue ConstructReplicated(TaleWorlds.CampaignSystem.Hero owner, TFields fields);
}
