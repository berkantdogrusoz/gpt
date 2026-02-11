using UnityEngine;
using System.Collections.Generic;

public class SlotSpawner : MonoBehaviour
{
    public SlotCell slotPrefab;
    public int slotCount = 3;
    public float spacing = 150f;

    private List<SlotCell> spawnedSlots = new List<SlotCell>();

    void Start()
    {
        SpawnSlots();
    }

    void SpawnSlots()
    {
        float total = (slotCount - 1) * spacing;
        float startX = -total * 0.5f;

        for (int i = 0; i < slotCount; i++)
        {
            SlotCell slot = Instantiate(slotPrefab, transform);
            spawnedSlots.Add(slot);

            RectTransform rt = slot.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = new Vector2(startX + i * spacing, 0);
            else
                slot.transform.localPosition = new Vector3(startX + i * spacing, 0, 0);
        }
    }
}