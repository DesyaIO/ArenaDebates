using UnityEngine;
using UnityEngine.SceneManagement;
public class ArenaSceneLink : MonoBehaviour
{
    public string SceneName;
    public bool OpenArchive;
    public void Open()
    {
        if(OpenArchive) SceneParams.OpenLearningPanelOnMenu=true;
        SceneManager.LoadScene(SceneName);
    }
}
