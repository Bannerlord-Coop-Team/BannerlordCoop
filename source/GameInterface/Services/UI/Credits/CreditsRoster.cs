using System.Collections.Generic;

namespace GameInterface.Services.UI.Credits;

/// <summary>
/// Names shown in the credits popup, one section per list. Add new names here; sections render
/// in roster order and an empty list shows a "coming soon" line instead of names.
/// </summary>
internal static class CreditsRoster
{
    /// <summary>Organizations and communities sponsoring the project.</summary>
    public static readonly IReadOnlyList<string> Sponsors = new[]
    {
        "骑砍中文站 (www.mountblade.com.cn)",
    };

    /// <summary>People who supported the project financially (donations, Patreon).</summary>
    public static readonly IReadOnlyList<string> Supporters = new[]
    {
        "Koung",
        "Lord Zippykins",
    };

    /// <summary>
    /// GitHub contributors ordered by contribution count
    /// (github.com/Bannerlord-Coop-Team/BannerlordCoop/graphs/contributors).
    /// </summary>
    public static readonly IReadOnlyList<string> Contributors = new[]
    {
        "Joke (Garrett)",
        "Andrew.Orlowski (ShoT-UP)",
        "EgardA (Hasted)",
        "Jordan Brymora (Curzek)",
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
}
