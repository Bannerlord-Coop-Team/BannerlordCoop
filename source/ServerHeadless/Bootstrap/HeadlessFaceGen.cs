using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace ServerHeadless.Bootstrap
{
    /// <summary>
    /// Managed stand-in for the native face generator (<see cref="FaceGen"/>'s engine-installed
    /// instance). Headless, <c>FaceGen._instance</c> is null: every lookup silently returns
    /// nothing, so generated heroes (notables, wanderers, children) end up with the bald low end
    /// of their template's appearance range, and location-character creation NREs on the missing
    /// <see cref="Monster"/> lookups.
    ///
    /// The face-key bit layout is native, so true interpolation/mutation of appearance keys is
    /// not reproducible here. Approximations chosen instead:
    /// - Appearance: pick the template's authored MIN or MAX key from the creation seed — always
    ///   a valid, author-designed face (max first: its hair/beard indices are rarely zero).
    /// - Dynamic properties (age/weight/build are plain floats): truly interpolate from the seed.
    /// - Monster lookups: resolve from MBObjectManager by id, the same objects the XML load
    ///   registered (fixes settlement population's LocationCharacter creation).
    /// - Key-mutating operations (SetHair/SetBody/SetPigmentation): no-ops, keys stay authored.
    /// </summary>
    internal sealed class HeadlessFaceGen : IFaceGen
    {
        private const string BaseHumanMonsterId = "human";

        /// <summary>Deterministic 0..1 from the creation seed (heroes regenerate consistently).</summary>
        private static float UnitRandom(int seed) => (Math.Abs(seed) % 1000) / 999f;

        public BodyProperties GetRandomBodyProperties(
            int race, bool isFemale,
            BodyProperties bodyPropertiesMin, BodyProperties bodyPropertiesMax,
            int hairCoverType, int seed,
            string hairTags, string beardTags, string tatooTags,
            float variationAmount)
        {
            float t = UnitRandom(seed);

            // Static key: whole authored endpoint (bit-packed, cannot be blended managed).
            var staticKey = (seed & 1) == 0
                ? bodyPropertiesMax.StaticProperties
                : bodyPropertiesMin.StaticProperties;

            // Dynamics are plain floats; interpolate for variety.
            var dynamics = new DynamicBodyProperties(
                bodyPropertiesMin.Age + (bodyPropertiesMax.Age - bodyPropertiesMin.Age) * t,
                bodyPropertiesMin.Weight + (bodyPropertiesMax.Weight - bodyPropertiesMin.Weight) * t,
                bodyPropertiesMin.Build + (bodyPropertiesMax.Build - bodyPropertiesMin.Build) * t);

            return new BodyProperties(dynamics, staticKey);
        }

        public void GenerateParentBody(BodyProperties childBodyProperties, int race,
            ref BodyProperties motherBodyProperties, ref BodyProperties fatherBodyProperties)
        {
            // Parents inherit the child's key: valid faces, plausible family resemblance.
            motherBodyProperties = childBodyProperties;
            fatherBodyProperties = childBodyProperties;
        }

        // Key-mutating operations write into the native bit-packed face key; leave keys authored.
        public void SetBody(ref BodyProperties bodyProperties, int build, int weight) { }
        public void SetHair(ref BodyProperties bodyProperties, int hair, int beard, int tattoo) { }
        public void SetPigmentation(ref BodyProperties bodyProperties, int skinColor, int hairColor, int eyeColor) { }

        public BodyProperties GetBodyPropertiesWithAge(ref BodyProperties originalBodyProperties, float age)
        {
            var dynamics = new DynamicBodyProperties(
                age, originalBodyProperties.Weight, originalBodyProperties.Build);
            return new BodyProperties(dynamics, originalBodyProperties.StaticProperties);
        }

        public BodyMeshMaturityType GetMaturityTypeWithAge(float age)
        {
            if (age < 6f) return BodyMeshMaturityType.Toddler;
            if (age < 10f) return BodyMeshMaturityType.Child;
            if (age < 14f) return BodyMeshMaturityType.Tween;
            if (age < 18f) return BodyMeshMaturityType.Teenager;
            return BodyMeshMaturityType.Adult;
        }

        // Only the human race exists without the native race registry.
        public int GetRaceCount() => 1;
        public int GetRaceOrDefault(string raceId) => 0;
        public string GetBaseMonsterNameFromRace(int race) => BaseHumanMonsterId;
        public string[] GetRaceNames() => new[] { BaseHumanMonsterId };

        public Monster GetMonster(string monsterID)
        {
            return MBObjectManager.Instance?.GetObject<Monster>(monsterID)
                ?? MBObjectManager.Instance?.GetObject<Monster>(BaseHumanMonsterId);
        }

        public Monster GetMonsterWithSuffix(int race, string suffix)
        {
            string baseName = GetBaseMonsterNameFromRace(race);
            return MBObjectManager.Instance?.GetObject<Monster>($"{baseName}_{suffix}")
                ?? GetMonster(baseName);
        }

        public Monster GetBaseMonsterFromRace(int race) => GetMonster(GetBaseMonsterNameFromRace(race));

        // No native hair/beard/tattoo index tables; empty means "no choices", callers guard.
        public int[] GetHairIndicesByTag(int race, int curGender, float age, string tag) => Array.Empty<int>();
        public int[] GetFacialIndicesByTag(int race, int curGender, float age, string tag) => Array.Empty<int>();
        public int[] GetTattooIndicesByTag(int race, int curGender, float age, string tag) => Array.Empty<int>();
        public float GetTattooZeroProbability(int race, int curGender, float age) => 1f;
    }
}
