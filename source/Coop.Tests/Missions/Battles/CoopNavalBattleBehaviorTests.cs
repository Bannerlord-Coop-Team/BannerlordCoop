#if COOP_NAVAL
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.AI.TeamAI;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class CoopNavalBattleBehaviorTests
{
    [Theory]
    [InlineData(-1, 0, false)]
    [InlineData(120, 111, false)]
    [InlineData(120, 110, true)]
    [InlineData(120, 0, true)]
    [InlineData(0, 120, false)]
    public void IsAgentCountDrop_FlagsOnlyALargeDropAfterTheFirstCount(int before, int after, bool expected)
    {
        Assert.Equal(expected, CoopNavalBattleBehavior.IsAgentCountDrop(before, after));
    }

    // R53 F4 review: vanilla's removal expires no ship list, so a team (the enemy's above all) kept listing a removed hull for
    // up to 5 s after a migration replace or a vanilla retreat. A removal now expires every list of every naval team.
    [Fact]
    public void ExpireShipQueries_ExpiresEveryShipListOfEveryNavalTeam_AndSkipsTeamsWithoutNavalAi()
    {
        var own = NavalTeam(out var ownQueries);
        var enemy = NavalTeam(out var enemyQueries);
        var landAi = (Team)RuntimeHelpers.GetUninitializedObject(typeof(Team));

        CoopNavalBattleBehavior.ExpireShipQueries(new[] { own, landAi, enemy });

        Assert.Equal(0f, ExpireTime(enemyQueries._enemyShipsInLeftToRightOrder));
        Assert.All(QueryDataOf(ownQueries).Concat(QueryDataOf(enemyQueries)), query => Assert.Equal(0f, ExpireTime(query)));
    }

    // A constructor-skipped team whose naval AI holds a query system with every list freshly cached at mission time 10.
    private static Team NavalTeam(out NavalQuerySystem queries)
    {
        queries = (NavalQuerySystem)RuntimeHelpers.GetUninitializedObject(typeof(NavalQuerySystem));
        foreach (var field in QueryFields())
        {
            var query = Activator.CreateInstance(field.FieldType, new object?[] { null, 5f });
            AccessTools.Method(field.FieldType, "SetValue").Invoke(query, new[] { DefaultOf(field.FieldType.GetGenericArguments()[0]), 10f });
            field.SetValue(queries, query);
        }

        var teamAi = (TeamAINavalComponent)RuntimeHelpers.GetUninitializedObject(typeof(TeamAINavalComponent));
        teamAi.TeamNavalQuerySystem = queries;
        var team = (Team)RuntimeHelpers.GetUninitializedObject(typeof(Team));
        AccessTools.Property(typeof(Team), nameof(Team.TeamAI)).SetValue(team, teamAi);
        return team;
    }

    // The query system's cached lists and positions.
    private static System.Reflection.FieldInfo[] QueryFields() =>
        AccessTools.GetDeclaredFields(typeof(NavalQuerySystem))
            .Where(field => field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(QueryData<>))
            .ToArray();

    // Every cached query of one query system.
    private static object[] QueryDataOf(NavalQuerySystem queries) => QueryFields().Select(field => field.GetValue(queries)).ToArray();

    // When a cached query next re-evaluates; zero means expired.
    private static float ExpireTime(object query) => (float)AccessTools.Field(query.GetType(), "_expireTime").GetValue(query);

    // The default value of a query's result type.
    private static object? DefaultOf(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
}
#endif
