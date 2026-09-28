using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DebateTopic", menuName = "Debates/Debate Topic")]
public class DebateTopicSO : ScriptableObject
{
    [Tooltip("Категория: Маркетинг, HR, IT и т.д.")]
    public string Category;

    [Tooltip("Формулировка темы дебатов")]
    [TextArea(3, 6)]
    public string Description;

    [Tooltip("Позиции сторон")]
    public List<PositionSO> Positions = new List<PositionSO>();
}