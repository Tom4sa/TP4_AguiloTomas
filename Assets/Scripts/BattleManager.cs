using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public class ItemStack
{
    public ItemData item;
    public int amount = 1;
}

public enum BattleState { Running, WaitingPlayer, Acting, Ended }

// Controla el combate: barras ATB, orden de actuacion (uno por vez), acciones, IA enemiga y fin de batalla.
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("Equipos (assets CharacterData)")]
    public CharacterData[] allyTeam;
    public CharacterData[] enemyTeam;
    [Tooltip("Posiciones donde aparece cada personaje (en el mismo orden que el equipo).")]
    public Transform[] allySlots;
    public Transform[] enemySlots;

    [Header("Acciones y objetos")]
    public SkillData basicAttack;
    public ItemStack[] startingItems;

    [Header("ATB")]
    [Tooltip("Multiplicador global de la velocidad con que se llenan las barras.")]
    public float atbRate = 1f;
    [Tooltip("Si esta activo, las barras se detienen mientras el jugador elige una accion.")]
    public bool waitWhileChoosing = true;

    [Header("Feedback")]
    public AudioSource audioSource;
    public AudioClip hitClip;
    public AudioClip healClip;
    public AudioClip victoryClip;
    public AudioClip defeatClip;
    [Tooltip("El que actua se acerca al objetivo al atacar (util si no hay animaciones).")]
    public bool useLunge = true;

    public List<Combatant> Allies { get; private set; }
    public List<Combatant> Enemies { get; private set; }
    public BattleState State { get; private set; }
    public Combatant Current { get; private set; }
    public SkillData BasicAttack { get { return basicAttack; } }

    public event Action<Combatant> OnPlayerTurn;
    public event Action<string> OnLog;
    public event Action<bool> OnBattleEnd;

    readonly Dictionary<ItemData, int> inventory = new Dictionary<ItemData, int>();

    // ---------- Inicio ----------

    void Awake()
    {
        Instance = this;
        Allies = new List<Combatant>();
        Enemies = new List<Combatant>();

        if (basicAttack == null) Debug.LogError("BattleManager: falta asignar el Basic Attack.");

        SpawnTeam(allyTeam, allySlots, true, Allies);
        SpawnTeam(enemyTeam, enemySlots, false, Enemies);

        if (startingItems != null)
            foreach (var s in startingItems)
            {
                if (s == null || s.item == null) continue;
                inventory[s.item] = GetItemCount(s.item) + s.amount;
            }

        State = BattleState.Running;
    }

    void SpawnTeam(CharacterData[] team, Transform[] slots, bool player, List<Combatant> list)
    {
        if (team == null) return;

        for (int i = 0; i < team.Length; i++)
        {
            CharacterData d = team[i];
            if (d == null) continue;

            // Si hay varios iguales (ej: 2 goblins) les agrega A, B...
            int total = 0, index = 0;
            for (int k = 0; k < team.Length; k++)
                if (team[k] == d) { total++; if (k < i) index++; }
            string displayName = total > 1 ? d.characterName + " " + (char)('A' + index) : d.characterName;

            Transform slot = (slots != null && i < slots.Length) ? slots[i] : null;
            Vector3 pos = slot != null ? slot.position : new Vector3(player ? -4f : 4f, 0f, (i - (team.Length - 1) / 2f) * 2.5f);
            Quaternion rot = slot != null ? slot.rotation : Quaternion.Euler(0f, player ? 90f : -90f, 0f);

            GameObject go;
            if (d.modelPrefab != null)
            {
                go = Instantiate(d.modelPrefab, pos, rot);
            }
            else
            {
                // Capsula de prueba si todavia no hay modelo
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.SetPositionAndRotation(pos + Vector3.up, rot);
                go.GetComponent<Renderer>().material.color = player ? new Color(0.3f, 0.5f, 1f) : new Color(1f, 0.4f, 0.3f);
            }
            go.name = displayName;

            Combatant c = go.GetComponent<Combatant>();
            if (c == null) c = go.AddComponent<Combatant>();
            c.Init(d, player, displayName);
            c.OnDied += HandleDeath;
            list.Add(c);
        }
    }

    // ---------- Bucle ATB ----------

    void Update()
    {
        bool ticking = State == BattleState.Running ||
                       (State == BattleState.WaitingPlayer && !waitWhileChoosing);

        if (ticking)
        {
            foreach (var c in Allies) c.Tick(Time.deltaTime, atbRate);
            foreach (var c in Enemies) c.Tick(Time.deltaTime, atbRate);
        }

        if (State == BattleState.Running)
        {
            Combatant next = null;
            foreach (var c in Allies) next = Better(next, c);
            foreach (var c in Enemies) next = Better(next, c);
            if (next != null) BeginTurn(next);
        }
    }

    // Entre los que tienen la barra llena, actua primero el de mayor ATB (y luego el mas veloz).
    Combatant Better(Combatant best, Combatant c)
    {
        if (!c.IsReady) return best;
        if (best == null) return c;
        if (c.Atb > best.Atb) return c;
        if (Mathf.Approximately(c.Atb, best.Atb) && c.GetStat(StatType.Speed) > best.GetStat(StatType.Speed)) return c;
        return best;
    }

    void BeginTurn(Combatant c)
    {
        Current = c;
        if (c.IsPlayerSide)
        {
            State = BattleState.WaitingPlayer;
            Log("¡Turno de " + c.DisplayName + "!");
            if (OnPlayerTurn != null) OnPlayerTurn(c);
        }
        else
        {
            State = BattleState.Acting;
            StartCoroutine(EnemyTurn(c));
        }
    }

    // ---------- API para la UI ----------

    public bool NeedsTargetChoice(ActionData a)
    {
        return a.target == TargetType.SingleEnemy || a.target == TargetType.SingleAlly;
    }

    public List<Combatant> GetValidTargets(Combatant actor, ActionData a)
    {
        var result = new List<Combatant>();
        List<Combatant> same = actor.IsPlayerSide ? Allies : Enemies;
        List<Combatant> opposite = actor.IsPlayerSide ? Enemies : Allies;

        switch (a.target)
        {
            case TargetType.SingleEnemy:
            case TargetType.AllEnemies:
                AddAlive(result, opposite); break;
            case TargetType.SingleAlly:
            case TargetType.AllAllies:
                AddAlive(result, same); break;
            case TargetType.Self:
                result.Add(actor); break;
        }
        return result;
    }

    static void AddAlive(List<Combatant> to, List<Combatant> from)
    {
        foreach (var c in from) if (c.IsAlive) to.Add(c);
    }

    public bool CanUse(Combatant actor, ActionData a)
    {
        var skill = a as SkillData;
        if (skill != null) return actor.Mp >= skill.mpCost;
        var item = a as ItemData;
        if (item != null) return GetItemCount(item) > 0;
        return true;
    }

    public int GetItemCount(ItemData item)
    {
        int n;
        return inventory.TryGetValue(item, out n) ? n : 0;
    }

    public List<ItemData> GetAvailableItems()
    {
        var list = new List<ItemData>();
        foreach (var kv in inventory) if (kv.Value > 0) list.Add(kv.Key);
        return list;
    }

    public void PlayerSelectedAction(ActionData action, List<Combatant> targets)
    {
        if (State != BattleState.WaitingPlayer || Current == null || action == null) return;
        if (!CanUse(Current, action)) return;
        StartCoroutine(ExecuteAction(Current, action, targets));
    }

    // ---------- Ejecucion de acciones ----------

    IEnumerator ExecuteAction(Combatant actor, ActionData a, List<Combatant> targets)
    {
        State = BattleState.Acting;

        // Costos
        var skill = a as SkillData;
        var item = a as ItemData;
        if (skill != null) actor.SpendMp(skill.mpCost);
        if (item != null) inventory[item] = Mathf.Max(0, GetItemCount(item) - 1);

        Log(actor.DisplayName + " usa " + a.actionName);
        actor.PlayTrigger(a.animTrigger);
        PlaySfx(a.sfx);

        bool offensive = a.target == TargetType.SingleEnemy || a.target == TargetType.AllEnemies;
        bool lunged = useLunge && offensive && targets.Count > 0;
        Vector3 start = actor.transform.position;
        if (lunged) yield return MoveTo(actor.transform, LungePoint(actor, targets[0]), 0.2f);

        yield return new WaitForSeconds(a.effectDelay);

        foreach (var t in targets)
            if (t != null && t.IsAlive) ApplyEffects(actor, a, t);

        yield return new WaitForSeconds(0.6f);
        if (lunged) yield return MoveTo(actor.transform, start, 0.2f);

        // Costo de recuperacion: cuanto ATB se gasta
        actor.ConsumeAtb(a.atbCost);
        Current = null;

        if (CheckEnd()) yield break;
        State = BattleState.Running;
    }

    void ApplyEffects(Combatant actor, ActionData a, Combatant t)
    {
        if (a.vfxPrefab != null)
        {
            var fx = Instantiate(a.vfxPrefab, t.transform.position + Vector3.up, Quaternion.identity);
            Destroy(fx, 2f);
        }

        if (a.power > 0f)
        {
            int dmg = CalculateDamage(actor, t, a.power);
            t.TakeDamage(dmg);
            Log(t.DisplayName + " recibe " + dmg + " de daño");
            PlaySfx(hitClip);
            if (!t.IsAlive) return;
        }

        if (a.healHp > 0)
        {
            int healed = t.Heal(a.healHp);
            Log(t.DisplayName + " recupera " + healed + " de vida");
            PlaySfx(healClip);
        }

        if (a.healMp > 0)
        {
            int restored = t.RestoreMp(a.healMp);
            Log(t.DisplayName + " recupera " + restored + " de maná");
            PlaySfx(healClip);
        }

        if (!Mathf.Approximately(a.atbChange, 0f))
        {
            t.AddAtb(a.atbChange);
            Log(t.DisplayName + (a.atbChange > 0f ? " se acelera" : " se ralentiza"));
        }

        if (a.modifyStat != StatType.None && !Mathf.Approximately(a.modifyAmount, 0f))
        {
            t.AddModifier(a.modifyStat, a.modifyAmount, a.modifyDuration);
            Log(t.DisplayName + ": " + StatModifier.ShortName(a.modifyStat) + " " + a.modifyAmount.ToString("+0%;-0%") + " por " + a.modifyDuration + "s");
        }
    }

    int CalculateDamage(Combatant attacker, Combatant defender, float power)
    {
        float raw = power * attacker.GetStat(StatType.Attack) * 100f / (100f + defender.GetStat(StatType.Defense));
        return Mathf.Max(1, Mathf.RoundToInt(raw * Random.Range(0.9f, 1.1f)));
    }

    // ---------- IA enemiga ----------

    IEnumerator EnemyTurn(Combatant e)
    {
        yield return new WaitForSeconds(0.7f);

        ActionData action;
        List<Combatant> targets;
        ChooseEnemyAction(e, out action, out targets);

        if (action == null || targets.Count == 0)
        {
            e.ConsumeAtb(100f);
            State = BattleState.Running;
            yield break;
        }
        yield return ExecuteAction(e, action, targets);
    }

    void ChooseEnemyAction(Combatant e, out ActionData action, out List<Combatant> targets)
    {
        var options = new List<ActionData>();
        if (basicAttack != null) options.Add(basicAttack);
        if (e.Data.skills != null)
            foreach (var s in e.Data.skills)
                if (s != null && e.Mp >= s.mpCost) options.Add(s);

        ActionData chosen = null;

        // 1) Si tiene poca vida y puede curarse, se cura
        if (e.HpPercent < 0.35f)
            foreach (var o in options)
                if (o.healHp > 0) { chosen = o; break; }

        // 2) Si no, elige al azar entre sus acciones ofensivas / de apoyo (sin curaciones)
        if (chosen == null)
        {
            var pool = new List<ActionData>();
            foreach (var o in options) if (o.healHp <= 0) pool.Add(o);
            if (pool.Count > 0) chosen = pool[Random.Range(0, pool.Count)];
        }

        action = chosen;
        targets = new List<Combatant>();
        if (chosen == null) return;

        targets = GetValidTargets(e, chosen);
        if (NeedsTargetChoice(chosen) && targets.Count > 0)
        {
            Combatant pick;
            if (chosen.target == TargetType.SingleAlly) pick = LowestHp(targets);
            else pick = Random.value < 0.6f ? LowestHp(targets) : targets[Random.Range(0, targets.Count)];
            targets = new List<Combatant> { pick };
        }
    }

    static Combatant LowestHp(List<Combatant> list)
    {
        Combatant best = list[0];
        foreach (var c in list) if (c.HpPercent < best.HpPercent) best = c;
        return best;
    }

    // ---------- Fin de batalla ----------

    void HandleDeath(Combatant c)
    {
        Log(c.DisplayName + " ha caído");
    }

    bool CheckEnd()
    {
        if (!AnyAlive(Enemies)) { End(true); return true; }
        if (!AnyAlive(Allies)) { End(false); return true; }
        return false;
    }

    static bool AnyAlive(List<Combatant> list)
    {
        foreach (var c in list) if (c.IsAlive) return true;
        return false;
    }

    void End(bool victory)
    {
        State = BattleState.Ended;
        Log(victory ? "¡Victoria!" : "Derrota...");
        PlaySfx(victory ? victoryClip : defeatClip);
        if (OnBattleEnd != null) OnBattleEnd(victory);
    }

    // ---------- Utilidades ----------

    void Log(string msg)
    {
        if (OnLog != null) OnLog(msg);
    }

    void PlaySfx(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    static Vector3 LungePoint(Combatant actor, Combatant target)
    {
        Vector3 dir = actor.transform.position - target.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
        Vector3 p = target.transform.position + dir.normalized * 1.6f;
        p.y = actor.transform.position.y;
        return p;
    }

    static IEnumerator MoveTo(Transform tr, Vector3 dest, float time)
    {
        Vector3 from = tr.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / time;
            tr.position = Vector3.Lerp(from, dest, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        tr.position = dest;
    }
}
