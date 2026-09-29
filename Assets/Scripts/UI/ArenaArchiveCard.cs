using TMPro;
using UnityEngine;

public class ArenaArchiveCard : MonoBehaviour
{
    // Native card data is filled by MenuController for the active goal.
    public TMP_Text Title, Number, Status;
    public void Bind(string title, int index, bool completed)
    {
        Title.text = title;
        Number.text = $"ДОСЬЕ / {index:000}";
        Status.text = completed ? "ПРОЧИТАНО" : "НЕ ПРОЧИТАНО";
    }
}
