using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves a leaderboard row's team name to its spawned car GameObject. The row's identity key is
/// CarIdentity.TeamName (host rows) / LeaderboardEntry.name (student rows) — both equal the name
/// CarSpawner assigns, so a single case-sensitive TeamName match covers both roles. Kept a pure
/// static (no MonoBehaviour state) so the match rule is unit-testable. Returns null on any miss /
/// blank input; never throws. When two teams share a name, returns the first (the leaderboard
/// already renders duplicate names as-is, so this mirrors what the clicked row shows).
/// </summary>
public static class CarLookup
{
    public static GameObject FindByTeamName(IReadOnlyList<GameObject> cars, string teamName)
    {
        if (cars == null || string.IsNullOrEmpty(teamName)) return null;

        for (int i = 0; i < cars.Count; i++)
        {
            GameObject go = cars[i];
            if (go == null) continue;
            var identity = go.GetComponent<CarIdentity>();
            if (identity != null && identity.TeamName == teamName) return go;
        }
        return null;
    }
}
