using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AnswerTypeDatabase", menuName = "Debates/Answer Type Database")]
public class AnswerTypeDatabaseSO : ScriptableObject
{
    public List<AnswerTypeSO> Types = new List<AnswerTypeSO>();

    public AnswerTypeSO GetByName(string typeName)
    {
        return Types.Find(t => t != null && Normalize(t.TypeName) == Normalize(typeName));
    }

    private static string Normalize(string name)
    {
        if (name == null) return "";
        return System.Text.RegularExpressions.Regex.Replace(
            name.Replace("**", "").Trim(' ', '\n', '\r', '[', ']', '"', '«', '»').ToLowerInvariant(),
            @"\s+", "").Replace("ё", "е");
    }

    public int GetDamage(string typeName)
    {
        var t = GetByName(typeName);
        return t != null ? t.DamageToOpponent : 0;
    }
}