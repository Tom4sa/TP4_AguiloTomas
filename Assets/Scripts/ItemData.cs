using UnityEngine;

// Objeto consumible: recupera vida/mana o aplica buffs a aliados / debuffs a enemigos.
// La cantidad inicial se configura en el BattleManager (startingItems).
[CreateAssetMenu(fileName = "Item", menuName = "RPG/Item")]
public class ItemData : ActionData
{
    void Reset()
    {
        target = TargetType.SingleAlly;
        atbCost = 50f;
        animTrigger = "Item";
    }
}
