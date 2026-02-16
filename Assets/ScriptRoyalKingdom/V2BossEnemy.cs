using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class V2BossEnemy : MonoBehaviour
{
    [Header("Boss")]
    public string bossName = "Boss";
    public int maxHp = 200;

    [Header("UI (Optional)")]
    public TMP_Text bossNameText;
    public TMP_Text hpText;
    public Slider hpSlider;

    private int currentHp;
    private bool dead;

    public bool IsDead => dead;
    public event Action OnBossDied;

    private void Start()
    {
        ResetBoss();
    }

    public void ResetBoss()
    {
        dead = false;
        currentHp = Mathf.Max(1, maxHp);
        RefreshUI();
    }

    public void ApplyDamage(int amount)
    {
        if (dead) return;

        int dmg = Mathf.Max(0, amount);
        if (dmg == 0) return;

        currentHp = Mathf.Max(0, currentHp - dmg);
        RefreshUI();

        if (currentHp <= 0)
        {
            dead = true;
            Debug.Log($"[V2Boss] {bossName} defeated!");
            OnBossDied?.Invoke();
        }
    }

    private void RefreshUI()
    {
        if (bossNameText != null)
            bossNameText.text = bossName;

        if (hpText != null)
            hpText.text = $"{currentHp}/{Mathf.Max(1, maxHp)}";

        if (hpSlider != null)
        {
            hpSlider.maxValue = Mathf.Max(1, maxHp);
            hpSlider.value = currentHp;
        }
    }
}