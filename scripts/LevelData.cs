using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Puzzle/Level Data", fileName = "LevelData_01")]
public class LevelData : ScriptableObject
{
    [Header("Board")]
    public int rows = 5;
    public int cols = 5;

    [Header("Pieces to Place (targets on grid)")]
    public List<TargetCell> targets = new List<TargetCell>();

    [Serializable]
    public struct TargetCell
    {
        public int row; // 0..rows-1
        public int col; // 0..cols-1
        public int pieceId; // unique id per piece in this level
    }
}
