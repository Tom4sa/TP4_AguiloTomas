using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ficha de un personaje en pantalla: nombre, vida, mana, barra ATB y buffs activos.
public class CombatantHUD : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text hpText;
    public TMP_Text mpText;
    public TMP_Text statusText;
    public Slider hpBar;
    public Slider mpBar;
    public Slider atbBar;
    [Tooltip("Marco o imagen que se prende cuando es el turno de este personaje.")]
    public GameObject highlight;
    [Tooltip("Opcional: se atenua cuando el personaje muere.")]
    public CanvasGroup canvasGroup;

    Combatant c;
    readonly StringBuilder sb = new StringBuilder();

    public void Bind(Combatant combatant)
    {
        c = combatant;
        if (nameText != null) nameText.text = c.DisplayName;
        hpBar.minValue = 0; hpBar.maxValue = c.MaxHp;
        if (mpBar != null) { mpBar.minValue = 0; mpBar.maxValue = Mathf.Max(1, c.MaxMp); }
        atbBar.minValue = 0; atbBar.maxValue = 100;
        SetHighlight(false);
    }

    public void SetHighlight(bool on)
    {
        if (highlight != null) highlight.SetActive(on);
    }

    void Update()
    {
        if (c == null) return;

        hpBar.value = c.Hp;
        if (hpText != null) hpText.text = "HP " + c.Hp + "/" + c.MaxHp;
        if (mpBar != null) mpBar.value = c.Mp;
        if (mpText != null) mpText.text = "MP " + c.Mp + "/" + c.MaxMp;
        atbBar.value = Mathf.Clamp(c.Atb, 0f, 100f);

        if (statusText != null)
        {
            sb.Length = 0;
            foreach (var m in c.Modifiers)
                sb.Append(m.Label).Append(' ').Append(Mathf.CeilToInt(m.remaining)).Append("s   ");
            statusText.text = sb.ToString();
        }

        if (canvasGroup != null) canvasGroup.alpha = c.IsAlive ? 1f : 0.4f;
    }
}
