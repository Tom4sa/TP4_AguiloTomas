using UnityEngine;

// Datos de un personaje (aliado o enemigo). Se edita desde el Inspector.
[CreateAssetMenu(fileName = "Character", menuName = "RPG/Character")]
public class CharacterData : ScriptableObject
{
    public string characterName = "Heroe";
    [Tooltip("Modelo 3D (con Animator opcional). Si queda vacio se usa una capsula de prueba.")]
    public GameObject modelPrefab;

    [Header("Estadisticas")]
    public int maxHp = 100;
    public int maxMp = 30;
    public int attack = 20;
    public int defense = 10;
    [Tooltip("Velocidad: define que tan rapido se llena su barra ATB.")]
    public float speed = 12f;

    [Header("Habilidades")]
    public SkillData[] skills;

    [Header("Feedback")]
    public AudioClip deathClip;
}
