using System.Collections.Generic;

namespace GameInterface.Services.UI.Credits;

/// <summary>
/// Names shown in the credits popup, one section per list. Add new names here; sections render
/// in roster order and an empty list shows a "coming soon" line instead of names.
/// </summary>
internal static class CreditsRoster
{
    /// <summary>
    /// GitHub contributors ordered by contribution count
    /// (github.com/Bannerlord-Coop-Team/BannerlordCoop/graphs/contributors).
    /// </summary>
    public static readonly IReadOnlyList<string> Contributors = new[]
    {
        "garrettluskey",
        "ShoT-UPfps",
        "EgardA",
        "jordanbrymora",
        "araex",
        "MaxDorob",
        "samuelzurowski",
        "NatteTosti69",
        "zzzzzzzzott",
        "WakooMan",
        "georgyrudnev",
        "Gerseras",
        "thomas-soutif",
        "masesk",
        "orfolei",
        "martinprejsa",
        "brodrigz",
        "Gears321",
        "evorios",
        "thomasDelaporte",
        "spk-berthel",
        "Allen-Glass",
        "dennispost99",
        "MaxBosse",
        "smudge202",
        "DiMiGi",
        "techratTV",
        "phong-nnguyen",
        "wesley-krug",
        "tristanka",
        "ignaciofernandezsoto",
        "GitEiko",
        "AndreasOhmer",
        "Magenstor",
        "code-factor",
        "Cytraen",
        "ac-jurd",
        "jacksonamartin",
        "hakanyildizhan",
        "Hollo1001",
        "Ceron257",
        "WesKrug",
        "BetterAtBrewing",
        "Anziverov",
        "kaefoody",
        "NicodemusU",
        "richlander",
    };

    /// <summary>Community members: moderators, translators, testers, and other helpers.</summary>
    public static readonly IReadOnlyList<string> Community = new string[0];

    /// <summary>People who supported the project financially (donations, Patreon).</summary>
    public static readonly IReadOnlyList<string> Supporters = new string[0];
}
