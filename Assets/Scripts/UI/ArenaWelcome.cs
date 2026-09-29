using UnityEngine;
using UnityEngine.UI;

public class ArenaWelcome : MonoBehaviour
{
    public GameObject WelcomePanel, ProfilePanel;
    public Button StartButton, ContinueButton, BackButton, PasswordVisibilityButton;
    public LoginPanel Login;
    void Awake()
    {
        StartButton.onClick.AddListener(() => OpenProfile(true));
        ContinueButton.onClick.AddListener(() => OpenProfile(false));
        BackButton.onClick.AddListener(() => { ProfilePanel.SetActive(false); WelcomePanel.SetActive(true); });
        PasswordVisibilityButton.onClick.AddListener(() =>
        {
            var field = Login.PasswordInput;
            field.contentType = field.contentType == TMPro.TMP_InputField.ContentType.Password
                ? TMPro.TMP_InputField.ContentType.Standard : TMPro.TMP_InputField.ContentType.Password;
            field.ForceLabelUpdate();
        });
    }
    void OpenProfile(bool registration)
    {
        WelcomePanel.SetActive(false);
        ProfilePanel.SetActive(true);
        Login.SetRegisterMode(registration);
    }
}
