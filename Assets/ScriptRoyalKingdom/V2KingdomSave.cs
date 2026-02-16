using UnityEngine;

public static class V2KingdomSave
{
    private const string Prefix = "v2_kingdom_";

    public static int GetInt(string key, int defaultValue = 0)
    {
        return PlayerPrefs.GetInt(Prefix + key, defaultValue);
    }

    public static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(Prefix + key, value);
        PlayerPrefs.Save();
    }

    public static bool GetBool(string key, bool defaultValue = false)
    {
        int def = defaultValue ? 1 : 0;
        return PlayerPrefs.GetInt(Prefix + key, def) == 1;
    }

    public static void SetBool(string key, bool value)
    {
        PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
