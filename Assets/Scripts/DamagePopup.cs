using TMPro;
using UnityEngine;

// Numero flotante (dano, curacion, buffs) que sube y se desvanece. Se crea por codigo, sin prefab.
public class DamagePopup : MonoBehaviour
{
    const float Life = 1.2f;
    TextMeshPro tmp;
    float t;

    public static void Spawn(Vector3 position, string text, Color color)
    {
        var go = new GameObject("Popup");
        go.transform.position = position;
        var tm = go.AddComponent<TextMeshPro>();
        tm.text = text;
        tm.color = color;
        tm.fontSize = 5f;
        tm.fontStyle = FontStyles.Bold;
        tm.alignment = TextAlignmentOptions.Center;
        tm.sortingOrder = 50;
        go.AddComponent<DamagePopup>().tmp = tm;
    }

    void Update()
    {
        t += Time.deltaTime;
        transform.position += Vector3.up * (1.2f * Time.deltaTime);

        var c = tmp.color;
        c.a = 1f - Mathf.Clamp01((t - 0.6f) / (Life - 0.6f));
        tmp.color = c;

        if (Camera.main != null) transform.rotation = Camera.main.transform.rotation;
        if (t >= Life) Destroy(gameObject);
    }
}
