# PersonalFlyout 🎵

A beautiful, custom WinUI 3 media overlay that replaces the default Windows media flyout with a modern, feature-rich interface featuring album art, dynamic colors, and smooth animations.

![WinUI 3](https://img.shields.io/badge/WinUI-3-blue)
![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple)
![Windows 10](https://img.shields.io/badge/Windows-11-0078D4)
![License](https://img.shields.io/badge/license-MIT-green)

---

## ✨ Features

### 🎨 **Dynamic Visual Experience**
- **Album Art Display**: Shows the current track's album artwork
- **Dominant Color Extraction**: Automatically extracts and applies the dominant color from album art as a gradient background
- **Smooth Animations**: 
  - Slide-in from left for next track
  - Slide-in from right for previous track
  - Fade-in for play/pause actions
  - Auto-hide after 4 seconds

### 🎛️ **Media Controls**
- **Playback Controls**: Previous, Play/Pause, Next buttons
- **Progress Bar**: Real-time track progress with current/total time display
- **Audio Visualizer**: Animated bars that pulse with playback

### ⌨️ **Keyboard Integration**
- **Media Key Support**: Responds to keyboard media keys (Play, Pause, Next, Previous)
- **Native Overlay Suppression**: Automatically suppresses the default Windows media overlay using low-level keyboard hooks

### 🪟 **Window Behavior**
- **Always on Top**: Stays visible above other windows
- **Auto-positioning**: Appears in the top-left corner (40px, 10px)
- **Borderless Design**: Rounded corners with no system border
- **Compact Size**: 400x120px footprint

---

## 🖼️ Screenshots

![PersonalFlyout in Action](Assets/Screenshots/Screenshot%202026-01-06%20232843.png)

*The custom media flyout displaying album art, playback controls, and dynamic gradient background*

---

## 🛠️ Technologies Used

- **Framework**: [WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/) (Windows App SDK 1.8)
- **Language**: C# with .NET 8.0
- **Target Platform**: Windows 10 (19041) and above
- **APIs Used**:
  - `GlobalSystemMediaTransportControls` - For media session management
  - `BitmapDecoder` - For color extraction from album art
  - Windows Hooks (`SetWindowsHookEx`) - For keyboard media key interception
  - DWM (Desktop Window Manager) - For borderless window styling

---

## 📋 Prerequisites

Before you begin, ensure you have the following installed:

1. **Windows 11** (or Windows 10 version 19041+)
2. **Visual Studio 2022** (17.0 or later) with:
   - .NET Desktop Development workload
   - Windows App SDK C# Templates
   - Windows 11 SDK (10.0.26100.0 or later)
3. **Windows App SDK 1.8** (installed via NuGet, included in project)

---

## 🚀 Getting Started

### Installation

1. **Clone the repository**
   ```powershell
   git clone https://github.com/SydBurhan/PersonalFlyout.git
   cd PersonalFlyout
   ```

2. **Open the solution**
   - Open `PersonalFlyout.sln` in Visual Studio 2022

3. **Restore NuGet packages**
   - Visual Studio should automatically restore packages
   - Or manually: Right-click solution → **Restore NuGet Packages**

4. **Select target platform**
   - Choose `x64`, `x86`, or `ARM64` from the platform dropdown

5. **Build the project**
   - Press `Ctrl+Shift+B` or go to **Build → Build Solution**

### Running the Application

#### Option 1: Debug Mode (Recommended for Development)
1. Press `F5` or click **Debug → Start Debugging**
2. The flyout will start minimized (off-screen)
3. Play any media (Spotify, YouTube, etc.) and press a media key to see the flyout

#### Option 2: Run Without Debugging
1. Press `Ctrl+F5` or click **Debug → Start Without Debugging**

#### Option 3: Published Executable
1. Build the project in **Release** mode
2. Navigate to `bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\`
3. Run `PersonalFlyout.exe`

---

## 🎮 Usage

### Basic Controls

- **Play/Pause**: Click the center button or press keyboard media play/pause key
- **Next Track**: Click the right arrow or press keyboard next track key
- **Previous Track**: Click the left arrow or press keyboard previous track key

### Behavior

1. **Startup**: The flyout shows a brief greeting ("Personal Flyout - Ready to play") for 3 seconds
2. **Media Playback**: When you play media or change tracks, the flyout appears automatically
3. **Auto-Hide**: The flyout disappears after 4 seconds of inactivity
4. **Animations**: 
   - Next track → Slides in from the left
   - Previous track → Slides in from the right
   - Play/Pause → Fades in

---

## 🔧 Configuration

### Suppressing Windows Native Overlay

The app automatically suppresses the Windows native media overlay using keyboard hooks. If you want to disable this:

1. Open `MainWindow.xaml.cs`
2. Comment out lines 83-95 (the `MediaOverlaySuppressor` initialization)

Alternatively, you can manually disable the Windows overlay using the registry:

1. Run `DisableMediaOverlay.reg` (included in the project)
2. Restart your computer

To re-enable the Windows overlay:
```reg
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\MediaControls]
"EnableMediaOverlay"=dword:00000001
```

### Customizing Appearance

**Window Size**:
```csharp
// In MainWindow.xaml.cs, line 79
_appWindow.Resize(new Windows.Graphics.SizeInt32(400, 120));
```

**Position**:
```csharp
// In MainWindow.xaml.cs, line 214
_appWindow.Move(new Windows.Graphics.PointInt32(40, 10)); // Top-left corner
```

**Auto-Hide Duration**:
```csharp
// In MainWindow.xaml.cs, line 141
_hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
```

---

## 📁 Project Structure

```
PersonalFlyout/
├── App.xaml                    # Application definition
├── App.xaml.cs                 # Application startup logic
├── MainWindow.xaml             # UI layout and animations
├── MainWindow.xaml.cs          # Main window logic and media controls
├── MediaOverlaySuppressor.cs   # Keyboard hook for media key interception
├── DisableMediaOverlay.reg     # Registry file to disable Windows overlay
├── Package.appxmanifest        # App package manifest
├── app.manifest                # Application manifest (admin privileges)
├── PersonalFlyout.csproj       # Project file
├── Assets/                     # App icons and images
└── Properties/                 # Publish profiles and launch settings
```

---

## 🧩 Key Components

### 1. **MainWindow.xaml.cs**
- Manages media session using `GlobalSystemMediaTransportControls`
- Extracts dominant color from album art
- Handles playback controls and UI updates
- Implements smooth show/hide animations

### 2. **MediaOverlaySuppressor.cs**
- Installs a low-level keyboard hook (`WH_KEYBOARD_LL`)
- Intercepts media keys (Play, Pause, Next, Previous)
- Suppresses the native Windows overlay
- Relays media commands to the main window for directional animations

### 3. **MainWindow.xaml**
- Defines the UI layout with album art, controls, and progress bar
- Contains three storyboard animations:
  - `ShowAnimation` (default fade-in)
  - `ShowAnimationNext` (slide from left)
  - `ShowAnimationPrev` (slide from right)
  - `HideAnimation` (fade-out)

---

## 🐛 Troubleshooting

### Flyout doesn't appear
- **Check if media is playing**: The flyout only appears when media is actively playing
- **Verify media app compatibility**: Works with Spotify, YouTube, Windows Media Player, etc.
- **Check Windows version**: Requires Windows 10 19041+ or Windows 11

### Native Windows overlay still appears
- Run `DisableMediaOverlay.reg` and restart your computer
- Ensure the app is running with administrator privileges (required for keyboard hooks)

### Build errors
- **Missing SDK**: Install Windows 11 SDK (10.0.26100.0) via Visual Studio Installer
- **NuGet restore failed**: Right-click solution → **Restore NuGet Packages**
- **Platform mismatch**: Ensure you're building for `x64`, `x86`, or `ARM64` (not `Any CPU`)

### Keyboard hooks not working
- **Run as Administrator**: Right-click `PersonalFlyout.exe` → **Run as administrator**
- The app requires elevated privileges to install global keyboard hooks

---

## 🏗️ Building for Release

### Create a Standalone Executable

1. **Set Configuration to Release**
   - In Visual Studio, change the configuration dropdown to **Release**

2. **Select Platform**
   - Choose `x64` (most common), `x86`, or `ARM64`

3. **Publish the App**
   ```powershell
   # From the project directory
   dotnet publish -c Release -r win-x64 --self-contained
   ```

4. **Locate the Executable**
   - Navigate to: `bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\`
   - Run `PersonalFlyout.exe`

### Optional: Create MSIX Package

1. In Visual Studio, right-click the project → **Publish → Create App Packages**
2. Follow the wizard to create an MSIX installer
3. Distribute the `.msix` file for easy installation

---

## 🤝 Contributing

Contributions are welcome! Here's how you can help:

1. **Fork the repository**
2. **Create a feature branch** (`git checkout -b feature/AmazingFeature`)
3. **Commit your changes** (`git commit -m 'Add some AmazingFeature'`)
4. **Push to the branch** (`git push origin feature/AmazingFeature`)
5. **Open a Pull Request**

### Ideas for Contributions
- Add support for custom themes
- Implement lyrics display
- Add gesture controls
- Create a settings panel
- Improve color extraction algorithm
- Add support for more media players

---

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- **Microsoft** - For WinUI 3 and Windows App SDK
- **Windows Community Toolkit** - For inspiration and best practices
- **Stack Overflow Community** - For troubleshooting help

---

## 📧 Contact

**Syed Burhan Ahmad** - [@SydBurhan](https://github.com/SydBurhan)

**Project Link**: [https://github.com/SydBurhan/PersonalFlyout](https://github.com/SydBurhan/PersonalFlyout)

---

## 🗺️ Roadmap

- [ ] Add settings UI for customization
- [ ] Implement multiple theme options
- [ ] Add lyrics support (via API integration)
- [ ] Create system tray icon with quick settings
- [ ] Add support for custom positions (corners, edges)
- [ ] Implement gesture controls for touchscreens
- [ ] Add Spotify/YouTube Music API integration for enhanced metadata
- [ ] Create installer package for easy distribution

---

<div align="center">

**Made with ❤️ using WinUI 3**

⭐ **Star this repo if you find it useful!** ⭐

</div>
