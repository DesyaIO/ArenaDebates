using System;
using UnityEngine;

[Serializable]
public class DialogueEntry
{
    public int Index;
    public bool IsPlayerTurn;          // true — ход игрока, false — оппонента
    public string UserText;            // текст реплики
    public string Category;            // тип ответа
    public string Explanation;         // пояснение от LLM
    public int Damage;                 // урон/лечение
    public int PlayerHealthAfter;      // HP игрока после хода
    public int OpponentHealthAfter;    // HP оппонента после хода
    public string Timestamp;

    public DialogueEntry() { }

    public DialogueEntry(int index, bool isPlayer, string text, string category,
                         string explanation, int damage, int playerHp, int opponentHp)
    {
        Index = index;
        IsPlayerTurn = isPlayer;
        UserText = text;
        Category = category;
        Explanation = explanation;
        Damage = damage;
        PlayerHealthAfter = playerHp;
        OpponentHealthAfter = opponentHp;
        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}