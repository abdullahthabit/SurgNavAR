using MixedReality.Toolkit.UX;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Manages application configurations and folders for AR surgical navigation.
/// Implements a Singleton pattern.
/// </summary>
public class Config : MonoBehaviour
{
    #region Singleton
    public static Config Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Ensure the entire GameObject is destroyed if duplicate
            return;
        }
        Instance = this;
        // script should persist across scenes
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    #region Application Folders
    private string rootApplicationFolder;
    private string streamingAssetsFolder;
    private string newApplicationName;

    [field: System.NonSerialized] public string ApplicationFolder { get; private set; }
    [field: System.NonSerialized] public string DemoApplicationFolder { get; private set; }
    [field: System.NonSerialized] public string MarkerTrackingFolder { get; private set; }
    [field: System.NonSerialized] public string PatientModelsFolder { get; private set; }
    [field: System.NonSerialized] public string CalibrationFolder { get; private set; }
    [field: System.NonSerialized] public string CalibrationFolderOut { get; private set; }
    [field: System.NonSerialized] public string RegistrationFolder { get; private set; }
    [field: System.NonSerialized] public string RegistrationFolderOut { get; private set; }
    #endregion

    #region Config File
    public ConfigFile configFile;
    private string configFilePath;

    [field: System.NonSerialized] public string ConfigData { get; private set; }
    [field: System.NonSerialized] public bool ConfigFileInitialized { get; private set; }
    #endregion

    #region Module Flags
    [field: System.NonSerialized] public bool IsNDIConnectionActive { get; set; } = false;
    [field: System.NonSerialized] public bool IsVuforiaTrackingActive { get; set; } = false;
    [field: System.NonSerialized] public bool IsArucoTrackingActive { get; set; } = false;
    [field: System.NonSerialized] public bool IsManualRegistrationActive { get; set; } = false;
    [field: System.NonSerialized] public bool IsLandmarkRegistrationActive { get; set; } = false;
    [field: System.NonSerialized] public bool IsToolCalibrationActive { get; set; } = false;
    #endregion

    #region Welcome Menu
    GameObject ExistingApplicationsDropDown;
    private TMP_Dropdown dropDownMenu;
    private List<string> applicationNames;
    [System.NonSerialized] public PressableButton GoToApplicationButton;
    #endregion

    // called from WelcomeSceneUIController.cs => start() 
    public void InitializeApplicationsList(GameObject dropdown)
    {
        if (dropdown == null)
        {
            Debug.LogError("ApplicationsDropDown is not assigned in the Inspector!");
            return;
        }
        ExistingApplicationsDropDown = dropdown;
        dropDownMenu = ExistingApplicationsDropDown.GetComponent<TMP_Dropdown>();
        LoadApplicationNames();
        PopulateDropDown();
    }

    #region Application Handling
    private void LoadApplicationNames()
    {
        streamingAssetsFolder = Application.streamingAssetsPath;
        rootApplicationFolder = Application.persistentDataPath;

        applicationNames = new List<string>();

        string[] subdirectories = Directory.GetDirectories(rootApplicationFolder);

        foreach (string dir in subdirectories)
        {
            string folderName = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(folderName))
                continue;

            string configPath = Path.Combine(dir, $"config_{folderName}.json");

            if (File.Exists(configPath))
            {
                applicationNames.Add(folderName);
            }
        }
    }

    private void PopulateDropDown()
    {
        if (applicationNames.Count > 0)
        {
            dropDownMenu.AddOptions(applicationNames);
            dropDownMenu.value = 0;
            UpdateExistingApplicationName();
        }
    }
    
    // called from UI -> InputFieldApplicationName
    public void UpdateNewApplicationName(string inputFieldText)
    {
        newApplicationName = inputFieldText;
        Debug.Log($"New application name: {newApplicationName}");
    }

    public void UpdateExistingApplicationName()
    {
        ApplicationFolder = Path.Combine(rootApplicationFolder, applicationNames[dropDownMenu.value]);
        configFilePath = Path.Combine(ApplicationFolder, $"config_{applicationNames[dropDownMenu.value]}.json");
    }

    // called from WelcomeSceneUIController.cs => start() 
    public void CreateNewApplication()
    {
        StartCoroutine(CreateNewApplicationRoutine());
    }
    private IEnumerator CreateNewApplicationRoutine()
    {
        if (string.IsNullOrWhiteSpace(newApplicationName))
        {
            Debug.LogWarning("Application name is empty. Checking if DemoApplication exists ....");
            newApplicationName = "DemoApplication";
            string applicationPath = Path.Combine(rootApplicationFolder, newApplicationName);
            if (Directory.Exists(applicationPath))
            {
                Debug.LogWarning("DemoApplication already exists, so we use it!!!");
                SetApplicationSubFolders();
                LoadConfigFile();
                yield break;
            }
        }

        ApplicationFolder = Path.Combine(rootApplicationFolder, newApplicationName);
        DemoApplicationFolder = Path.Combine(streamingAssetsFolder, "DemoApplication");
        configFilePath = Path.Combine(ApplicationFolder, $"config_{newApplicationName}.json");

        // use UnityWebRequest to compy DemoApplication
        yield return StartCoroutine(CopyDirectoryWithWebRequests(DemoApplicationFolder, ApplicationFolder));
        Debug.Log("Folder copy complete using UnityWebRequest.");

        SetApplicationSubFolders();
        LoadConfigFile();
    }

    // called from WelcomeSceneUIController.cs => start() 
    public void LoadExistingApplication()
    {
        SetApplicationSubFolders();
        LoadConfigFile();
    }

    private void SetApplicationSubFolders()
    {
        MarkerTrackingFolder = CreateFolder("marker_tracking");
        PatientModelsFolder = CreateFolder("patient_models");
        CalibrationFolder = CreateFolder("calibration");
        CalibrationFolderOut = CreateFolder(Path.Combine("calibration", "output_calibration"));
        RegistrationFolder = CreateFolder("registration");
        RegistrationFolderOut = CreateFolder(Path.Combine("registration", "output_registration"));
    }

    private string CreateFolder(string relativePath)
    {
        string fullPath = Path.Combine(ApplicationFolder, relativePath);
        if (!Directory.Exists(fullPath))
            Directory.CreateDirectory(fullPath);
        return fullPath;
    }
    #endregion

    #region Config File Handling
    private void LoadConfigFile()
    {
        if (!File.Exists(configFilePath))
        {
            Debug.LogWarning($"Config file not found at {configFilePath}");
            return;
        }

        ConfigData = File.ReadAllText(configFilePath);
        ConfigData = Utils.StripComments(ConfigData);
        configFile = JsonUtility.FromJson<ConfigFile>(ConfigData);
        ConfigFileInitialized = true;

        Debug.Log("Config: Loaded config parameters from file");

        ActivateGoToApplicationButton();
    }

    public void SaveConfigFile()
    {
        ConfigData = JsonUtility.ToJson(configFile, true);
        File.WriteAllText(configFilePath, ConfigData);

        Debug.Log("Config: Saved config parameters to file");
    }

    public void ClearConfigFile()
    {
        configFile =null;
        ConfigData = null;
        configFilePath = null;
        ConfigFileInitialized = false;
    }
    #endregion

    private void ActivateGoToApplicationButton()
    {
        GoToApplicationButton.enabled = true;
    }

    // Copy DemoApplication using UnityWebRequest
    private IEnumerator CopyDirectoryWithWebRequests(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        // Step 1: Build the manifest URI correctly (always forward slashes)
        string manifestUri = sourceDir;
        if (!manifestUri.EndsWith("/")) manifestUri += "/";
        manifestUri += "manifest.txt";

        // Step 2: Request manifest
        using (UnityWebRequest manifestRequest = UnityWebRequest.Get(manifestUri))
        {
            yield return manifestRequest.SendWebRequest();

            if (manifestRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to load manifest.txt from {manifestUri}: {manifestRequest.error}");
                yield break;
            }

            string[] fileList = manifestRequest.downloadHandler.text
                .Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

            // Step 3: Iterate files
            foreach (string relativePath in fileList)
            {
                string trimmedPath = relativePath.Trim();
                if (string.IsNullOrEmpty(trimmedPath))
                    continue;

                string ext = Path.GetExtension(trimmedPath).ToLower();
                if (ext == ".meta" || ext == ".ds_store")
                    continue;

                // Build safe URI for StreamingAssets
                string fileUri = sourceDir;
                if (!fileUri.EndsWith("/")) fileUri += "/";
                fileUri += trimmedPath.Replace("\\", "/"); // ensure forward slashes

                using (UnityWebRequest fileRequest = UnityWebRequest.Get(fileUri))
                {
                    yield return fileRequest.SendWebRequest();

                    if (fileRequest.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"Failed to copy {trimmedPath} from {fileUri}: {fileRequest.error}");
                        continue;
                    }

                    // Build destination on writable path
                    string destPath = Path.Combine(targetDir, trimmedPath);
                    string destDir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    string fileName = Path.GetFileName(trimmedPath);
                    if (fileName == "config_DemoApplication.json")
                        fileName = $"config_{newApplicationName}.json";

                    string finalDest = Path.Combine(destDir, fileName);
                    File.WriteAllBytes(finalDest, fileRequest.downloadHandler.data);
                    Debug.Log($"Copied {trimmedPath} → {finalDest}");
                }
            }
        }

        Debug.Log("Copy complete (manifest-based, safe URI build).");
    }

}
