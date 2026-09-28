using UnityEngine;

[CreateAssetMenu(fileName = "Position", menuName = "Debates/Position")]
public class PositionSO : ScriptableObject
{
    [Tooltip("Буква позиции: А или Б")]
    public string Label;

    [Tooltip("Короткое название позиции")]
    public string ShortName;

    [Tooltip("Полная формулировка позиции")]
    [TextArea(2, 5)]
    public string Description;
}