using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Interfaz del combate: fichas de personajes, menu de acciones, listas de habilidades/objetos/objetivos,
// registro de mensajes y paneles de victoria / derrota. No usa el teclado: todo por botones.
public class BattleUI : MonoBehaviour
{
    public BattleManager battle;

    [Header("Fichas de personajes")]
    public CombatantHUD hudPrefab;
    public Transform allyHudContainer;
    public Transform enemyHudContainer;

    [Header("Menu principal de acciones")]
    public GameObject actionPanel;
    public Button attackButton;
    public Button skillsButton;
    public Button itemsButton;
    public TMP_Text turnText;

    [Header("Lista (habilidades / objetos / objetivos)")]
    public GameObject listPanel;
    public Transform listContainer;
    public Button optionButtonPrefab;
    public Button backButton;
    public TMP_Text listTitle;

    [Header("Registro y fin de combate")]
    public TMP_Text logText;
    public GameObject victoryPanel;
    public GameObject defeatPanel;
    public Button[] restartButtons;

    Combatant actor;
    readonly Dictionary<Combatant, CombatantHUD> huds = new Dictionary<Combatant, CombatantHUD>();
    readonly Queue<string> logLines = new Queue<string>();

    void Start()
    {
        if (battle == null) battle = BattleManager.Instance;

        foreach (var c in battle.Allies) CreateHud(c, allyHudContainer);
        foreach (var c in battle.Enemies) CreateHud(c, enemyHudContainer);

        battle.OnPlayerTurn += ShowActionMenu;
        battle.OnLog += ShowLog;
        battle.OnBattleEnd += ShowEnd;

        attackButton.onClick.AddListener(() => BeginAction(battle.BasicAttack));
        skillsButton.onClick.AddListener(ShowSkillList);
        itemsButton.onClick.AddListener(ShowItemList);
        backButton.onClick.AddListener(() => ShowActionMenu(actor));
        if (restartButtons != null)
            foreach (var b in restartButtons) b.onClick.AddListener(Restart);

        actionPanel.SetActive(false);
        listPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (logText != null) logText.text = "";
    }

    void OnDestroy()
    {
        if (battle == null) return;
        battle.OnPlayerTurn -= ShowActionMenu;
        battle.OnLog -= ShowLog;
        battle.OnBattleEnd -= ShowEnd;
    }

    void CreateHud(Combatant c, Transform container)
    {
        var hud = Instantiate(hudPrefab, container);
        hud.Bind(c);
        huds[c] = hud;
    }

    void SetHighlight(Combatant c)
    {
        foreach (var kv in huds) kv.Value.SetHighlight(kv.Key == c);
    }

    // ---------- Menu principal ----------

    void ShowActionMenu(Combatant c)
    {
        actor = c;
        SetHighlight(c);
        actionPanel.SetActive(true);
        listPanel.SetActive(false);
        if (turnText != null) turnText.text = "Turno de " + c.DisplayName;

        attackButton.interactable = true;
        skillsButton.interactable = c.Data.skills != null && c.Data.skills.Length > 0;
        itemsButton.interactable = battle.GetAvailableItems().Count > 0;
    }

    // ---------- Listas ----------

    void OpenList(string title)
    {
        actionPanel.SetActive(false);
        listPanel.SetActive(true);
        if (listTitle != null) listTitle.text = title;
        foreach (Transform child in listContainer) Destroy(child.gameObject);
    }

    Button AddOption(string label, UnityEngine.Events.UnityAction onClick)
    {
        var b = Instantiate(optionButtonPrefab, listContainer);
        var text = b.GetComponentInChildren<TMP_Text>();
        if (text != null) text.text = label;
        b.onClick.AddListener(onClick);
        return b;
    }

    void ShowSkillList()
    {
        OpenList("Habilidades");
        foreach (var s in actor.Data.skills)
        {
            if (s == null) continue;
            var skill = s;
            var b = AddOption(skill.actionName + "  (MP " + skill.mpCost + ")", () => BeginAction(skill));
            b.interactable = battle.CanUse(actor, skill);
        }
    }

    void ShowItemList()
    {
        OpenList("Objetos");
        foreach (var i in battle.GetAvailableItems())
        {
            var item = i;
            AddOption(item.actionName + "  x" + battle.GetItemCount(item), () => BeginAction(item));
        }
    }

    void BeginAction(ActionData action)
    {
        if (battle.NeedsTargetChoice(action)) ShowTargetList(action);
        else Confirm(action, battle.GetValidTargets(actor, action));
    }

    void ShowTargetList(ActionData action)
    {
        OpenList("Elegí un objetivo");
        foreach (var t in battle.GetValidTargets(actor, action))
        {
            var target = t;
            AddOption(target.DisplayName + "  (" + target.Hp + "/" + target.MaxHp + ")",
                () => Confirm(action, new List<Combatant> { target }));
        }
    }

    void Confirm(ActionData action, List<Combatant> targets)
    {
        actionPanel.SetActive(false);
        listPanel.SetActive(false);
        SetHighlight(null);
        battle.PlayerSelectedAction(action, targets);
    }

    // ---------- Registro y fin ----------

    void ShowLog(string msg)
    {
        if (logText == null) return;
        logLines.Enqueue(msg);
        while (logLines.Count > 4) logLines.Dequeue();
        logText.text = string.Join("\n", logLines.ToArray());
    }

    void ShowEnd(bool victory)
    {
        actionPanel.SetActive(false);
        listPanel.SetActive(false);
        SetHighlight(null);
        var panel = victory ? victoryPanel : defeatPanel;
        if (panel != null) panel.SetActive(true);
    }

    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
