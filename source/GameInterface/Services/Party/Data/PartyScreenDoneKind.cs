namespace GameInterface.Services.Party.Data;

/// <summary>
/// Which vanilla <c>PartyScreenHelper</c> done handler the screen used, so the server runs its effects.
/// </summary>
internal enum PartyScreenDoneKind : byte
{
    // A custom handler or one with no effects, like ManageTroopsAndPrisonersDoneHandler.
    None,
    // DefaultDoneHandler: prisoners left on a client-only left side are released, ones taken from it are captured.
    Default,
    SellPrisoners,
    DonateGarrison,
    DonatePrisoners,
    ManageGarrison,
    // OpenScreenAsCreateClanPartyForHero: the troops moved left become the new party, built when the screen closes in vanilla.
    CreateClanParty,
}
