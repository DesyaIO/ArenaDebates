using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class LoginPanel : MonoBehaviour
{
    [Header("Поля ввода")]
    public TMP_InputField LoginInput;
    public TMP_InputField PasswordInput;
    public TMP_InputField FullNameInput; // Только для регистрации

    [Header("Кнопки")]
    public Button LoginButton;
    public Button RegisterButton;

    [Header("Уведомления")]
    public TMP_Text MessageText;

    [Header("Панель регистрации")]
    public GameObject RegisterFieldsPanel; // Панель с полем ФИО — скрыта по умолчанию

    private bool _isRegisterMode = false;

    void Start()
    {
        LoginButton.onClick.AddListener(OnLogin);
        RegisterButton.onClick.AddListener(OnToggleRegister);

        RegisterFieldsPanel.SetActive(false);
        MessageText.text = "";
    }

    void OnToggleRegister()
    {
        _isRegisterMode = !_isRegisterMode;
        RegisterFieldsPanel.SetActive(_isRegisterMode);

        LoginButton.GetComponentInChildren<TMP_Text>().text = _isRegisterMode ? "Зарегистрироваться" : "Войти";
        RegisterButton.GetComponentInChildren<TMP_Text>().text = _isRegisterMode ? "У меня уже есть аккаунт" : "Регистрация";
        MessageText.text = "";
    }

    void OnLogin()
    {
        string login = LoginInput.text.Trim();
        string password = PasswordInput.text;

        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        {
            MessageText.text = "Заполните логин и пароль.";
            return;
        }

        if (_isRegisterMode)
        {
            string fullName = FullNameInput.text.Trim();
            if (string.IsNullOrEmpty(fullName))
            {
                MessageText.text = "Введите ФИО.";
                return;
            }

            var user = UserManager.Register(login, password, fullName);
            if (user == null)
            {
                MessageText.text = "Пользователь с таким логином уже существует.";
                return;
            }

            MessageText.text = "Регистрация успешна!";
            GoToNextScene();
        }
        else
        {
            var user = UserManager.Login(login, password);
            if (user == null)
            {
                MessageText.text = "Неверный логин или пароль.";
                return;
            }

            MessageText.text = "Вход выполнен!";
            GoToNextScene();
        }
    }

    void GoToNextScene()
    {
        var user = UserManager.CurrentUser;

        if (user == null || !user.HasActiveGoal())
            SceneManager.LoadScene("GoalSetupScene");
        else
            SceneManager.LoadScene("MenuScene");
    }
}