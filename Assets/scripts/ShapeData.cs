using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Puzzle/Shape Data", fileName = "Shape_")]
public class ShapeData : ScriptableObject
{
    [Header("Shape Info")]
    public string shapeName = "L-Shape";
    public Color shapeColor = Color.white;
    
    [Header("Pattern (X ile işaretle, boşluk = boş)")]
    [Tooltip("Her satır yeni line, X = dolu hücre, boşluk = boş hücre")]
    [TextArea(5, 10)]
    public string pattern = 
@"X
X
X X";
    
    [Header("Visual")]
    public Sprite cellSprite; // Her küçük kare için sprite
    
    // Pattern'i parse et ve hücre pozisyonlarını döndür
    public List<Vector2Int> GetCells()
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        
        if (string.IsNullOrEmpty(pattern))
            return cells;

        // Normalize CRLF -> LF ve split
        var rows = pattern.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        
        for (int r = 0; r < rows.Length; r++)
        {
            string row = rows[r];
            if (string.IsNullOrWhiteSpace(row)) continue;
            
            for (int c = 0; c < row.Length; c++)
            {
                char ch = row[c];
                if (ch == 'X' || ch == 'x')
                {
                    cells.Add(new Vector2Int(c, r));
                }
            }
        }
        
        return cells;
    }
    
    // Shape'in genişlik/yüksekliğini hesapla
    public Vector2Int GetSize()
    {
        List<Vector2Int> cells = GetCells();
        if (cells.Count == 0) return Vector2Int.zero;
        
        int maxX = 0;
        int maxY = 0;
        
        foreach (var cell in cells)
        {
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y > maxY) maxY = cell.y;
        }
        
        return new Vector2Int(maxX + 1, maxY + 1);
    }
}