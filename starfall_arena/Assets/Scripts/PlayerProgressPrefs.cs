using UnityEngine;

public static class PlayerProgressPrefs
{
    private const string HasWonInvasionModeKey = "StarfallArena.HasWonInvasionMode";

    public static bool HasWonInvasionMode => PlayerPrefs.GetInt(HasWonInvasionModeKey, 0) == 1;

    public static void MarkInvasionModeWon()
    {
        if (HasWonInvasionMode)
        {
            return;
        }

        PlayerPrefs.SetInt(HasWonInvasionModeKey, 1);
        PlayerPrefs.Save();
    }
}
