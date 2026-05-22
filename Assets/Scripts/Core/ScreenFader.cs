using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 전체를 부드럽게 페이드 인/아웃 시켜주는 전역 싱글톤 페이더 컴포넌트.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    private static ScreenFader instance;
    public static ScreenFader Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<ScreenFader>();
                if (instance == null)
                {
                    GameObject go = new GameObject("ScreenFader");
                    instance = go.AddComponent<ScreenFader>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    private Canvas faderCanvas;
    private Image faderImage;
    private Coroutine currentFadeCoroutine;

    public bool IsFading => currentFadeCoroutine != null;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        SetupFaderUI();
    }

    private void SetupFaderUI()
    {
        // Create fader canvas
        GameObject canvasGo = new GameObject("FaderCanvas");
        canvasGo.transform.SetParent(transform);
        
        faderCanvas = canvasGo.AddComponent<Canvas>();
        faderCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        faderCanvas.sortingOrder = 9999; // Always draw on top

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGo.AddComponent<GraphicRaycaster>();

        // Create solid fader black image
        GameObject imageGo = new GameObject("FaderImage");
        imageGo.transform.SetParent(canvasGo.transform, false);

        faderImage = imageGo.AddComponent<Image>();
        faderImage.color = new Color(0f, 0f, 0f, 0f); // Initially fully transparent

        // Fill complete screen bounds
        RectTransform rect = faderImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        // Block raycasts during full dark fade
        faderImage.raycastTarget = false;
    }

    public Coroutine FadeOut(float duration)
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        currentFadeCoroutine = StartCoroutine(FadeRoutine(1f, duration));
        return currentFadeCoroutine;
    }

    public Coroutine FadeIn(float duration)
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        currentFadeCoroutine = StartCoroutine(FadeRoutine(0f, duration));
        return currentFadeCoroutine;
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (faderImage == null) SetupFaderUI();

        // Block user inputs/clicks if fading to dark or staying dark
        faderImage.raycastTarget = targetAlpha > 0.01f;

        float startAlpha = faderImage.color.a;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            faderImage.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }

        faderImage.color = new Color(0f, 0f, 0f, targetAlpha);
        faderImage.raycastTarget = targetAlpha > 0.01f;
        currentFadeCoroutine = null;
    }
}
