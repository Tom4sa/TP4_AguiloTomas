using UnityEngine;

// Habilidad: accion que consume mana.
[CreateAssetMenu(fileName = "Skill", menuName = "RPG/Skill")]
public class SkillData : ActionData
{
    [Header("Habilidad")]
    public int mpCost = 5;
}
