using UnityEngine;

[CreateAssetMenu(fileName = "ListeningTask", menuName = "Learning/Listening Task")]
public class ListeningTaskSO : ScriptableObject
{
    [Tooltip("Уникальный ID задачи (например, harvard_01, batna_03)")]
    public string TaskId;

    [Tooltip("Аудиоклип с диалогом/репликой")]
    public AudioClip AudioClip;

    [Tooltip("Правильный тип ответа (Гарвардский метод, BATNA, SPIN и т.д.)")]
    public string CorrectMethodology;

    [Tooltip("Обоснование, почему это именно так")]
    [TextArea(3, 8)]
    public string CorrectExplanation;

    [Tooltip("Варианты для радиобаттонов (должны включать CorrectMethodology)")]
    public string[] AvailableMethodologies;
}