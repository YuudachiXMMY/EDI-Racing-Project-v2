using System;
using System.Collections.Generic;

/// <summary>
/// Pure parser for the professor host-launch URL hash
/// ("…/#role=host&token=…&survey=…"). Kept free of MonoBehaviour/UnityEngine so it is
/// unit-testable in EditMode. Never throws; unknown/empty input yields an empty map.
/// </summary>
public static class HostLaunchParams
{
    public static Dictionary<string, string> ParseHash(string url)
    {
        var dict = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(url)) return dict;

        int hash = url.IndexOf('#');
        string frag = hash >= 0 ? url.Substring(hash + 1) : "";
        if (frag.StartsWith("?")) frag = frag.Substring(1);

        foreach (var pair in frag.Split('&'))
        {
            if (pair.Length == 0) continue;
            int eq = pair.IndexOf('=');
            if (eq <= 0) continue; // skip valueless or leading-'=' fragments
            string key = Uri.UnescapeDataString(pair.Substring(0, eq));
            string val = Uri.UnescapeDataString(pair.Substring(eq + 1));
            dict[key] = val;
        }
        return dict;
    }
}
