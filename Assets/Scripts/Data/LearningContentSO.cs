using UnityEngine;

[CreateAssetMenu(fileName = "LearningContent", menuName = "Learning/Content")]
public class LearningContentSO : ScriptableObject
{
    public string MethodologyName;
    [TextArea(5, 15)] public string Description;
    [TextArea(10, 30)] public string Examples;
}