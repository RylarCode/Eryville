using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Moves between scenes with a fade to black and back, so the switch is not a flash. Builds its own full-screen cover
// and survives the scene change, so nothing has to be set up in any scene: SceneTransition.Go("GameScene").
public class SceneTransition : MonoBehaviour
{
    public const float FadeOutSeconds = 0.4f;
    public const float FadeInSeconds = 0.5f;

    private static SceneTransition instance;

    private Image cover;
    private bool busy;

    // True from the moment Go is called until the new scene has faded in
    public static bool Busy => instance != null && instance.busy;

    // Fades out, loads the scene, fades in. Ignored while a transition is already running.
    public static void Go(string sceneName)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("SceneTransition");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SceneTransition>();
            instance.Build();
        }
        if (instance.busy) return;
        instance.busy = true;
        instance.StartCoroutine(instance.Run(sceneName));
    }

    private void Build()
    {
        GameObject canvasObject = new GameObject("Cover", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // over everything, including the performance panel

        GameObject imageObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        cover = imageObject.GetComponent<Image>();
        cover.color = new Color(0f, 0f, 0f, 0f);
        cover.raycastTarget = false;
        cover.enabled = false;
    }

    private IEnumerator Run(string sceneName)
    {
        cover.enabled = true;
        cover.raycastTarget = true; // taps do nothing while it is going dark

        for (float t = 0f; t < FadeOutSeconds; t += Time.unscaledDeltaTime)
        {
            SetAlpha(t / FadeOutSeconds);
            yield return null;
        }
        SetAlpha(1f);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        while (load != null && !load.isDone) yield return null;
        yield return null; // one frame for the new scene's managers to start before it is shown

        for (float t = 0f; t < FadeInSeconds; t += Time.unscaledDeltaTime)
        {
            SetAlpha(1f - t / FadeInSeconds);
            yield return null;
        }
        SetAlpha(0f);
        cover.raycastTarget = false;
        cover.enabled = false;
        busy = false;
    }

    private void SetAlpha(float alpha)
    {
        cover.color = new Color(0f, 0f, 0f, Mathf.Clamp(alpha, 0f, 1f));
    }
}
