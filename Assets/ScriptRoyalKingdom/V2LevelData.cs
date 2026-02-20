using UnityEngine;

[CreateAssetMenu(fileName = "V2LevelData", menuName = "V2/Level Data")]
public class V2LevelData : ScriptableObject
{
    public int rows = 8;
    public int cols = 8;
    [Range(3, 8)] public int colorCount = 5;
    public int moveLimit = 25;
    public int targetScore = 2500;

    [Header("Grid Shape (Optional)")]
    [Tooltip("Satır bazlı maske. Boş bırakılırsa tüm hücreler açıktır. '1' = açık, '0'/'x'/'.' = kapalı.")]
    public string[] rowMask;

    public bool IsCellPlayable(int row, int col)
    {
        if (row < 0 || col < 0 || row >= rows || col >= cols)
            return false;

        if (rowMask == null || rowMask.Length == 0)
            return true;

        if (row >= rowMask.Length)
            return true;

        string line = rowMask[row];
        if (string.IsNullOrEmpty(line) || col >= line.Length)
            return true;

        char ch = line[col];
        return ch != '0' && ch != 'x' && ch != 'X' && ch != '.';
    }
}