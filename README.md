# SurgNavAR: An Augmented Reality Surgical Navigation Framework for Optical See-Through Head Mounted Displays

> **An open-source mixed reality framework for HMD-based surgical navigation**, supporting both the **Microsoft HoloLens 2** and **Magic Leap 2**.
> Built with **Unity**, this framework provides functionalities for patient and tools tracking, tools calibration, patient model visualization, image-to-patient registration, and surgical guidance.



## 📖 Table of Contents

1. [Overview](#-overview)
2. [Features](#-features)
3. [Supported Platforms](#-supported-platforms)
4. [Repository Structure](#-repository-structure)
5. [Quick Start (Users)](#-quick-start-users)
6. [Quick Start (Developers)](#-quick-start-developers)
7. [Documentation](#-documentation)
8. [Citation](#-citation)


## 🧩 Overview

**SurgNavAR** is a stand-alone HMD-based AR navigation framework that requires only the HMD device for navigation. It is generalizable, application-agnostic and device-agnostic. It integrates various methods for tracking reference markers, calibrating surgical instruments, aligning preoperative models with the patient and visualizing target structures. The modules of the framework allow it to be configurable and generalizable to multiple surgical applications.



## ✨ Features

| Category           | Description                                                  |
| ------------------ | ------------------------------------------------------------ |
| **Tracking**       | Marker-based tracking using Vuforia and ArUco markers     |
| **Calibration**    | Surgical tool calibration using pivoting and template-based methods |
| **Registration**   | Point-based and manual-placement registration methods           |
| **Visualization**  | Load and render patient-specific 3D models with multiple target structures  |
| **Navigation**     | Show real-time guidance with virtual elements and numeric feedback display     |
| **Multi-platform** | Supports both the HoloLens 2 (UWP) and Magic Leap 2 (Android)             |
| **Configurable**     | adaptable to various surgical applications using config files |
| **Extensible**     | Modular architecture for including new algorithms and (possibly) devices |

---

## 🧠 Build Platforms

* **Microsoft HoloLens 2**
* **Magic Leap 2**
* Unity version: **2022.3.34f1**
* XR SDKs supported:

  * **Mixed Reality Toolkit (MRTK3)**
  * **OpenXR**

## 🚀 Quick Start (Users)

For users who want to **run the application** on the HL2/ML2 device:

1. Download Builds
- [ML2 APK v0.1.0](https://github.com/abdullahthabit/SurgNavAR/releases/download/0.1.0/SurgNavAR_0.1.0.0.apk)
- [HL2 AppxBundle v0.1.0](https://github.com/abdullahthabit/SurgNavAR/releases/download/0.1.0/SurgNavAR_0.1.0.0_arm64.appxbundle)

3. Copy to device via:

   * HoloLens → *Device Portal*
   * Magic Leap → *Magic Leap Hub*
4. Inside the device, locate the copied app file and install.
5. Follow setup and usage instructions in the [User Guide](./Docs/UserGuide.md).



## 🧑‍💻 Quick Start (Developers)

For researchers or contributors who want to **build or modify the source**:

1. Clone the repository:

   ```bash
   git clone git@github.com:abdullahthabit/SurgNavAR.git
   ```
2. Open Unity Hub, add an existing prject from desk by locating the cloned folder
2. Open in **Unity 2022.3.34f1**
4. Build for your target:

   * **UWP (HoloLens)** or **Android (Magic Leap)**
5. See [Developer Guide](./Docs/DeveloperGuide.md) for full setup details.


## 📚 Documentation

* [User Guide](./Docs/UserGuide.md) — Installation and app usage
* [Developer Guide](./Docs/DeveloperGuide.md) — Project setup, code structure, and contributions



## 🤝 Contributing

See [Developer Guide](./Docs/DeveloperGuide.md)

## 🧾 Citation

If you use this framework in your research, please cite:

```bibtex
@misc{ARNavFramework2026,
  title = {AR Surgical Navigation Framework},
  author = {Your Name and Contributors},
  year = {2026},
  howpublished = {\url{https://github.com/YOUR_USERNAME/AR-Surgical-Navigation-Framework}},
  note = {Open-source AR framework for surgical navigation research}
}
```