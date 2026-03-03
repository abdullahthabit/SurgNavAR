using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;

public class VuforiaInitialize : MonoBehaviour
{
    public void InitializeVuforiaEngine()
    {
        if (VuforiaApplication.Instance.IsInitialized) { return; }
        VuforiaConfiguration.Instance.Vuforia.MaxSimultaneousImageTargets = Config.Instance.configFile.unityConfig.vuforia.numberOfImageTargets;
        VuforiaApplication.Instance.Initialize();
    }
}
