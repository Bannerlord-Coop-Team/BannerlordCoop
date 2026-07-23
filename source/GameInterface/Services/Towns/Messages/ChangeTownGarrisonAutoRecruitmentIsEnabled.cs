using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the GarrisonAutoRecruitmentIsEnabled changes in a Town.
    /// </summary>
    public record ChangeTownGarrisonAutoRecruitmentIsEnabled : ICommand
    {
        public string TownId { get; }
        public bool GarrisonAutoRecruitmentIsEnabled { get; }

        public ChangeTownGarrisonAutoRecruitmentIsEnabled(string townId, bool garrisonAutoRecruitmentIsEnabled)
        {
            TownId = townId;
            GarrisonAutoRecruitmentIsEnabled = garrisonAutoRecruitmentIsEnabled;
        }
    }
}
