using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class V2StartupLoadingScreen : MonoBehaviour
{
    [Header("UI")]
    public GameObject loadingRoot;
    public Slider progressBar;
    public TMP_Text progressText;
    public TMP_Text madeByUnityText;

    [Header("Flow")]
    [Tooltip("Açılışta yüklenecek ana sahne. Boşsa aktif sahne içinde sadece loading panel kısa süre gösterilir.")]
    public string mainSceneName = "KingdomMenuScene";
    [Tooltip("Loading panelin ekranda kalacağı minimum süre.")]
    public float minShowDuration = 1.2f;
    [Tooltip("Loading bittiğinde panel kapanır. false ise panel açık kalır.")]
    public bool hideWhenDone = true;

    [Header("Branding")]
    public bool showMadeByUnity = true;
    public string madeByUnityLabel = "Made with Unity";

    private void Start()
    {
        StartCoroutine(BootFlow());
    }

    private IEnumerator BootFlow()
    {
        SetLoadingVisible(true);
        UpdateBranding();
        SetProgress(0f);

        float timer = 0f;

        if (!string.IsNullOrEmpty(mainSceneName) && !IsCurrentScene(mainSceneName))
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(mainSceneName);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                timer += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(op.progress / 0.9f);
                SetProgress(p);
                yield return null;
            }

            while (timer < minShowDuration)
            {
                timer += Time.unscaledDeltaTime;
                SetProgress(Mathf.Lerp(0.9f, 1f, timer / Mathf.Max(0.01f, minShowDuration)));
                yield return null;
            }

            SetProgress(1f);
            op.allowSceneActivation = true;
            yield break;
        }

        while (timer < minShowDuration)
        {
            timer += Time.unscaledDeltaTime;
            SetProgress(Mathf.Clamp01(timer / Mathf.Max(0.01f, minShowDuration)));
            yield return null;
        }

        SetProgress(1f);

        if (hideWhenDone)
            SetLoadingVisible(false);
    }

    private void SetProgress(float p)
    {
        if (progressBar != null)
            progressBar.value = p;

        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(p * 100f)}%";
    }

    private void UpdateBranding()
    {
        if (madeByUnityText == null)
            return;

        madeByUnityText.gameObject.SetActive(showMadeByUnity);
        if (showMadeByUnity)
            madeByUnityText.text = madeByUnityLabel;
    }

    private void SetLoadingVisible(bool visible)
    {
        if (loadingRoot != null)
            loadingRoot.SetActive(visible);
    }

    private bool IsCurrentScene(string sceneName)
    {
        return SceneManager.GetActiveScene().name == sceneName;
    }
}