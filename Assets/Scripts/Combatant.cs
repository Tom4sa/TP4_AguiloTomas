using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Buff o debuff temporal sobre una estadistica.
[Serializable]
public class StatModifier
{
    public StatType stat;
    public float amount;      // 0.5 = +50%, -0.3 = -30%
    public float remaining;   // segundos restantes

    public string Label
    {
        get { return ShortName(stat) + " " + amount.ToString("+0%;-0%"); }
    }

    public static string ShortName(StatType s)
    {
        switch (s)
        {
            case StatType.Attack: return "ATK";
            case StatType.Defense: return "DEF";
            case StatType.Speed: return "VEL";
            default: return "";
        }
    }
}

// Estado en combate de un personaje: vida, mana, barra ATB, buffs, animaciones y feedback visual.
public class Combatant : MonoBehaviour
{
    public CharacterData Data { get; private set; }
    public bool IsPlayerSide { get; private set; }
    public string DisplayName { get; private set; }

    public int Hp { get; private set; }
    public int Mp { get; private set; }
    public float Atb { get; private set; }   // 0..100 (puede ser negativa tras una accion costosa)

    public int MaxHp { get { return Data.maxHp; } }
    public int MaxMp { get { return Data.maxMp; } }
    public bool IsAlive { get { return Hp > 0; } }
    public bool IsReady { get { return IsAlive && Atb >= 100f; } }
    public float HpPercent { get { return MaxHp > 0 ? (float)Hp / MaxHp : 0f; } }
    public IReadOnlyList<StatModifier> Modifiers { get { return modifiers; } }

    public event Action<Combatant> OnDied;

    readonly List<StatModifier> modifiers = new List<StatModifier>();
    readonly HashSet<string> triggers = new HashSet<string>();
    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<Color> baseColors = new List<Color>();
    Animator anim;
    Coroutine flashRoutine;

    Vector3 PopupPos { get { return transform.position + Vector3.up * 2.3f; } }

    public void Init(CharacterData data, bool playerSide, string displayName)
    {
        Data = data;
        IsPlayerSide = playerSide;
        DisplayName = displayName;
        Hp = data.maxHp;
        Mp = data.maxMp;
        Atb = UnityEngine.Random.Range(0f, 40f);   // para que no actuen todos a la vez

        anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
            foreach (var p in anim.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger) triggers.Add(p.name);

        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            renderers.Add(r);
            baseColors.Add(ReadColor(r.sharedMaterial));
        }
    }

    // ---------- ATB y buffs ----------

    public void Tick(float dt, float atbRate)
    {
        if (!IsAlive) return;

        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            modifiers[i].remaining -= dt;
            if (modifiers[i].remaining <= 0f) modifiers.RemoveAt(i);
        }

        if (Atb < 100f)
            Atb = Mathf.Min(100f, Atb + GetStat(StatType.Speed) * atbRate * dt);
    }

    public float GetStat(StatType stat)
    {
        float baseValue;
        switch (stat)
        {
            case StatType.Attack: baseValue = Data.attack; break;
            case StatType.Defense: baseValue = Data.defense; break;
            case StatType.Speed: baseValue = Data.speed; break;
            default: baseValue = 0f; break;
        }
        float mult = 1f;
        foreach (var m in modifiers) if (m.stat == stat) mult += m.amount;
        return Mathf.Max(1f, baseValue * Mathf.Max(0.1f, mult));
    }

    public void ConsumeAtb(float cost)
    {
        Atb = Mathf.Max(-200f, Atb - cost);
    }

    public void AddAtb(float amount)
    {
        if (!IsAlive) return;
        Atb = Mathf.Clamp(Atb + amount, -200f, 100f);
        DamagePopup.Spawn(PopupPos, amount > 0 ? "¡Acelerado!" : "¡Ralentizado!",
            amount > 0 ? new Color(1f, 0.9f, 0.3f) : new Color(0.7f, 0.5f, 1f));
    }

    public void AddModifier(StatType stat, float amount, float duration)
    {
        if (!IsAlive) return;

        StatModifier existing = null;
        foreach (var m in modifiers)
            if (m.stat == stat && Mathf.Sign(m.amount) == Mathf.Sign(amount)) { existing = m; break; }

        if (existing != null) { existing.amount = amount; existing.remaining = duration; }
        else
        {
            existing = new StatModifier { stat = stat, amount = amount, remaining = duration };
            modifiers.Add(existing);
        }
        DamagePopup.Spawn(PopupPos, existing.Label,
            amount > 0 ? new Color(0.4f, 1f, 0.5f) : new Color(0.9f, 0.4f, 1f));
    }

    // ---------- Vida y mana ----------

    public void TakeDamage(int dmg)
    {
        if (!IsAlive) return;
        Hp = Mathf.Max(0, Hp - dmg);
        DamagePopup.Spawn(PopupPos, dmg.ToString(), new Color(1f, 0.35f, 0.3f));
        Flash(new Color(1f, 0.2f, 0.2f));
        if (Hp == 0) Die();
        else PlayTrigger("Hit");
    }

    public int Heal(int amount)
    {
        if (!IsAlive) return 0;
        int before = Hp;
        Hp = Mathf.Min(MaxHp, Hp + amount);
        int healed = Hp - before;
        DamagePopup.Spawn(PopupPos, "+" + healed, new Color(0.4f, 1f, 0.4f));
        Flash(new Color(0.4f, 1f, 0.4f));
        return healed;
    }

    public int RestoreMp(int amount)
    {
        if (!IsAlive) return 0;
        int before = Mp;
        Mp = Mathf.Min(MaxMp, Mp + amount);
        int restored = Mp - before;
        DamagePopup.Spawn(PopupPos, "+" + restored + " MP", new Color(0.4f, 0.7f, 1f));
        Flash(new Color(0.4f, 0.7f, 1f));
        return restored;
    }

    public void SpendMp(int amount)
    {
        Mp = Mathf.Max(0, Mp - amount);
    }

    void Die()
    {
        Atb = 0f;
        modifiers.Clear();
        if (triggers.Contains("Die")) PlayTrigger("Die");
        else StartCoroutine(FallOver());
        if (Data.deathClip != null) AudioSource.PlayClipAtPoint(Data.deathClip, transform.position);
        if (OnDied != null) OnDied(this);
    }

    IEnumerator FallOver()
    {
        Quaternion from = transform.rotation;
        Quaternion to = from * Quaternion.Euler(-90f, 0f, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.4f;
            transform.rotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }
    }

    // ---------- Animacion y feedback visual ----------

    public void PlayTrigger(string trigger)
    {
        if (anim == null || string.IsNullOrEmpty(trigger) || !triggers.Contains(trigger)) return;
        anim.SetTrigger(trigger);
    }

    void Flash(Color c)
    {
        if (renderers.Count == 0) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(c));
    }

    IEnumerator FlashRoutine(Color c)
    {
        for (int i = 0; i < renderers.Count; i++) WriteColor(renderers[i].material, c);
        yield return new WaitForSeconds(0.15f);
        for (int i = 0; i < renderers.Count; i++) WriteColor(renderers[i].material, baseColors[i]);
        flashRoutine = null;
    }

    static Color ReadColor(Material m)
    {
        if (m == null) return Color.white;
        if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
        if (m.HasProperty("_Color")) return m.GetColor("_Color");
        return Color.white;
    }

    static void WriteColor(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }
}
