using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the GarrisonAutoRecruitmentIsEnabled changes in a Town.
    /// </summary>
    public record TownGarrisonAutoRecruitmentIsEnabledChanged: ICommand
    {
        public string TownId { get; }
        public bool GarrisonAutoRecruitmentIsEnabled { get; }

        public TownGarrisonAutoRecruitmentIsEnabledChanged(string townId, bool garrisonAutoRecruitmentIsEnabled)
        {
            TownId = townId;
            GarrisonAutoRecruitmentIsEnabled = garrisonAutoRecruitmentIsEnabled;
        }
    }
}
