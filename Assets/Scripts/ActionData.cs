using UnityEngine;

public enum TargetType { SingleEnemy, AllEnemies, SingleAlly, AllAllies, Self }
public enum StatType { None, Attack, Defense, Speed }

// Base de habilidades y objetos: ambos son "acciones" configurables desde el Inspector.
// Una accion puede combinar varios efectos (dano, curacion, ATB, buff/debuff).
public abstract class ActionData : ScriptableObject
{
    public string actionName = "Accion";
    [TextArea] public string description;
    [Tooltip("Relativo a quien usa la accion: 'Enemy' = el equipo contrario.")]
    public TargetType target = TargetType.SingleEnemy;

    [Header("Efectos (dejar en 0 lo que no se use)")]
    [Tooltip("Multiplicador del ataque del usuario. 0 = no hace dano.")]
    public float power = 0f;
    public int healHp = 0;
    public int healMp = 0;
    [Tooltip("Positivo = acelera la barra ATB del objetivo. Negativo = la retrasa.")]
    public float atbChange = 0f;
    public StatType modifyStat = StatType.None;
    [Tooltip("0.5 = +50% (buff), -0.3 = -30% (debuff)")]
    public float modifyAmount = 0f;
    [Tooltip("Duracion del buff/debuff en segundos de combate.")]
    public float modifyDuration = 10f;

    [Header("Costo de recuperacion")]
    [Tooltip("Cuanta barra ATB se gasta al usarla. 100 = normal. Mas de 100 = tarda mas en volver a actuar. Menos de 100 = vuelve a actuar antes.")]
    public float atbCost = 100f;

    [Header("Feedback")]
    [Tooltip("Nombre del Trigger del Animator (ej: Attack, Skill). Si el personaje no lo tiene, se ignora.")]
    public string animTrigger = "Attack";
    [Tooltip("Segundos entre que empieza la animacion y se aplican los efectos.")]
    public float effectDelay = 0.4f;
    public AudioClip sfx;
    public GameObject vfxPrefab;   // particulas que aparecen sobre cada objetivo
}
