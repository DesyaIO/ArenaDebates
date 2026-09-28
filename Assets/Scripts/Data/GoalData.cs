using System.Collections.Generic;

[System.Serializable]
public class GoalEntry
{
    public string GoalText;
    public List<string> Methods = new List<string>();
}

[System.Serializable]
public class GoalIndustry
{
    public string Industry;
    public List<GoalEntry> Goals = new List<GoalEntry>();
}

[System.Serializable]
public class GoalDatabase
{
    public List<GoalIndustry> Industries = new List<GoalIndustry>();

    public GoalIndustry GetIndustry(string name)
    {
        return Industries.Find(i => i.Industry == name);
    }

    /// <summary>
    /// Возвращает список методов для конкретной цели.
    /// </summary>
    public List<string> GetMethodsForGoal(string industry, string goalText)
    {
        var ind = GetIndustry(industry);
        if (ind == null) return new List<string>();

        var goal = ind.Goals.Find(g => g.GoalText == goalText);
        return goal != null ? goal.Methods : new List<string>();
    }
}