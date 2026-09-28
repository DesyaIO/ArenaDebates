using UnityEngine;

[CreateAssetMenu(fileName = "AnswerType", menuName = "Debates/Answer Type")]
public class AnswerTypeSO : ScriptableObject
{
    [Tooltip("Название типа (Гарвардский метод, BATNA и т.д.)")]
    public string TypeName;

    [Tooltip("Описание для промпта и UI")]
    [TextArea(2, 5)]
    public string Description;

    [Tooltip("Урон оппоненту. Отрицательное — урон, положительное — лечение оппонента")]
    public int DamageToOpponent;

    [Tooltip("Цвет для UI-подсветки")]
    public Color DisplayColor = Color.white;
}