using UnityEngine;

[CreateAssetMenu(fileName = "V2LevelData", menuName = "V2/Level Data")]
public class V2LevelData : ScriptableObject
{
    public int rows = 8;
    public int cols = 8;
    [Range(3, 8)] public int colorCount = 5;
    public int moveLimit = 25;
    public int targetScore = 2500;
}
