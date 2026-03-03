using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
public class SceneLoader : MonoBehaviour
{
    // called from WelcomeSceneUIController
    public void LoadApplicationScene()
    {
        if (Config.Instance.ConfigFileInitialized)
        {
            SceneManager.LoadScene("SampleScene");
            Debug.Log("loading Application scene ...");
        }
        else
            Debug.LogError("No application was loaded... create new or load an existing application first!");

    }

    // called from a UI button
    public void LoadWelcomeScene()
    {
        // reset config file and load welcome scene
        Config.Instance.ClearConfigFile();
        SceneManager.LoadScene("WelcomeScene");
        Debug.Log("loading Welcome scene ...");
    }
}
