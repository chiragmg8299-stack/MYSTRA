using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BookInteraction : MonoBehaviour
{
    public Transform player;
    public Light glow;
    public Transform bookVisual;
    public float range = 2.5f;
    public string nextSceneName = "Hampi";
    public string titleText = "HAMPI — 1520 CE";

    bool busy, near;
    float fade, titleAlpha;
    Vector3 basePos;
    GUIStyle promptStyle, titleStyle;

    void Start() { basePos = bookVisual.localPosition; }

    void Update()
    {
        // idle floating + pulsing glow
        bookVisual.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * 1.5f) * 0.04f;
        bookVisual.Rotate(0, 20f * Time.deltaTime, 0, Space.Self);
        if (glow) glow.intensity = (busy ? 8f : 2.5f) + Mathf.Sin(Time.time * 3f) * 0.8f;

        near = !busy && Vector3.Distance(player.position, transform.position) < range;
        if (near && MystraInput.InteractPressed()) StartCoroutine(TimeTravel());
    }

    IEnumerator TimeTravel()
    {
        busy = true;
        var pc = player.GetComponent<PlayerController>();
        if (pc) pc.frozen = true;

        for (float t = 0; t < 2.5f; t += Time.deltaTime)
        {
            fade = Mathf.SmoothStep(0, 1, t / 2.5f);
            bookVisual.Rotate(0, 400f * t * Time.deltaTime, 0, Space.Self);
            yield return null;
        }
        fade = 1;
        for (float t = 0; t < 1f; t += Time.deltaTime) { titleAlpha = t; yield return null; }
        titleAlpha = 1;
        yield return new WaitForSeconds(2.5f);

        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.Log("Scene '" + nextSceneName + "' not created/added to Build Settings yet.");
            yield return new WaitForSeconds(1.5f);
            titleAlpha = 0; fade = 0; busy = false;
            if (pc) pc.frozen = false;
        }
    }

    void OnGUI()
    {
        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 64, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
        if (near) GUI.Label(new Rect(0, Screen.height * 0.75f, Screen.width, 50), "[E] Interact", promptStyle);

        if (fade > 0)
        {
            GUI.color = new Color(0.95f, 0.85f, 0.6f, fade);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.1f, 0.05f, titleAlpha);
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), titleText, titleStyle);
            GUI.color = Color.white;
        }
    }
}