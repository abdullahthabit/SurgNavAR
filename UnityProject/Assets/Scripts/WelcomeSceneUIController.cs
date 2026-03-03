using MixedReality.Toolkit.UX;
using TMPro;
using UnityEngine;

public class WelcomeSceneUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private PressableButton createNewApplicationButton;
    [SerializeField] private PressableButton loadExistingApplicationButton;
    [SerializeField] private PressableButton runLoadedApplicationButton;
    [SerializeField] private GameObject applicationsDropDown;

    // Start is called before the first frame update
    void Start()
    {
        createNewApplicationButton.OnClicked.AddListener(() => Config.Instance.CreateNewApplication());
        createNewApplicationButton.OnClicked.AddListener(() => this.GetComponent<VuforiaInitialize>().InitializeVuforiaEngine());
        loadExistingApplicationButton.OnClicked.AddListener(() => Config.Instance.LoadExistingApplication());
        loadExistingApplicationButton.OnClicked.AddListener(() => this.GetComponent<VuforiaInitialize>().InitializeVuforiaEngine());
        runLoadedApplicationButton.OnClicked.AddListener(() => this.GetComponent<SceneLoader>().LoadApplicationScene());
        applicationsDropDown.GetComponent<TMP_Dropdown>().onValueChanged.AddListener(_ => Config.Instance.UpdateExistingApplicationName());

        Config.Instance.InitializeApplicationsList(applicationsDropDown);

        Config.Instance.GoToApplicationButton = runLoadedApplicationButton;
        runLoadedApplicationButton.enabled = false;
    }
}
