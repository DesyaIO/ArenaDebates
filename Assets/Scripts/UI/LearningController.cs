using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LearningController : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text TitleText;
    public TMP_Text DescriptionText;
    public TMP_Text ExamplesText;
    public Button CompleteButton;
    public Button BackButton;

    [Header("Контент")]
    public LearningContentSO[] AllContent;

    private LearningContentSO _current;

    void Start()
    {
        string methodName = SceneParams.SelectedLearningMethod;

        if (string.IsNullOrEmpty(methodName))
        {
            Debug.LogError("Не передано имя методологии!");
            SceneManager.LoadScene("MenuScene");
            return;
        }

        _current = System.Array.Find(AllContent, c => c.MethodologyName == methodName);

        if (_current == null)
        {
            Debug.LogError($"Контент для методологии '{methodName}' не найден!");
            return;
        }

        TitleText.text = _current.MethodologyName;
        DescriptionText.text = _current.Description;
        ExamplesText.text = _current.Examples;

        CompleteButton.onClick.AddListener(OnComplete);
        BackButton.onClick.AddListener(() => { SceneParams.OpenLearningPanelOnMenu = true; SceneManager.LoadScene("MenuScene"); });
    }

    void OnComplete()
    {
        PlayerProgress.MarkLearningCompleted(_current.MethodologyName);
        // После изучения возвращаемся к списку методов, чтобы пользователь
        // мог сразу продолжить обучение или выбрать следующий модуль.
        SceneParams.OpenLearningPanelOnMenu = true;
        SceneManager.LoadScene("MenuScene");
    }
}
