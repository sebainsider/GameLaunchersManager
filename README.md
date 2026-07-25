# LauncherBridge 🚀

<p align="center">
  <img src="LauncherBridge/icon.png" width="128" alt="LauncherBridge Icon" />
</p>

**LauncherBridge** is a lightweight, zero-dependency .NET 9 utility for Windows designed to solve Steam's inability to detect when games launched via third-party launchers (Epic Games Store, EA App, Ubisoft Connect, Battle.net, GOG Galaxy, etc.) exit.

---

## 💡 The Problem & The Solution

### The Problem
When you add a non-Steam game or custom launcher shortcut to Steam, Steam launches the launcher (e.g. Epic Games Launcher). When the game starts, the launcher process stays running in the background. As far as Steam is concerned, the "game" has started and never finishes, keeping your status locked to "In-Game" forever.

### The Solution (Smart Auto-Detection 🧠)
LauncherBridge automates process detection with zero configuration required for 95%+ of games:

1. **Snapshot**: Takes a snapshot of all active system processes.
2. **Launch**: Executes the specified game URI or launcher command.
3. **Smart Auto-Detect**: Monitors process creation, ignores known background launcher processes (`EpicGamesLauncher`, `EADesktop`, `UbisoftConnect`, `Battle.net`, etc.), and detects the newly spawned game executable (e.g. `AlanWake2.exe`).
4. **Lifetime Tracking**: Tracks the game process until all instances exit, then exits cleanly with exit code `0`, allowing Steam to accurately record play time and update your status!

---

## ⚡ Quick Start & Usage

### Basic Usage (Auto-Detection)
```cmd
LauncherBridge.exe "com.epicgames.launcher://apps/Item?action=launch"
```
*(You can also explicitly pass `--launch` / `-l`)*:
```cmd
LauncherBridge.exe --launch "com.epicgames.launcher://apps/Item?action=launch" --close-launcher
```

### Options & Parameters

| Parameter | Short | Required | Description | Default |
|---|---|---|---|---|
| `--launch` | `-l` | **Yes*** | Launcher URI or command line to execute (*can also be passed directly as 1st positional argument) | N/A |
| `--process` | `-p` | No | Explicit process name (without `.exe`) to override auto-detection | Auto-detect |
| `--timeout` | `-t` | No | Timeout in seconds waiting for the game process to start | `60` |
| `--close-launcher` | `-c` | No | Close third-party launcher (e.g. Epic Games Launcher) when game exits | `false` |
| `--verbose` | `-v` | No | Enable detailed debug logs | `false` |
| `--help` | `-h` | No | Display help message | N/A |

---

## 🎮 Steam Integration Examples

To use LauncherBridge with Steam:

1. Download or publish `LauncherBridge.exe`.
2. Place `LauncherBridge.exe` in a convenient directory (e.g., `C:\Tools\LauncherBridge.exe`).
3. In Steam, click **Games** -> **Add a Non-Steam Game to My Library...**
4. Select `LauncherBridge.exe`.
5. Right-click the newly added shortcut in Steam -> **Properties**.
6. Set the **Target** and **Launch Options** as shown in the examples below:

---

### 1. Epic Games Store
Launch games via Epic Games Store URIs (optionally add `--close-launcher` / `-c` to close Epic Launcher when done):

- **Target**: `"C:\Tools\LauncherBridge.exe"`
- **Launch Options**: `--launch "com.epicgames.launcher://apps/6f438871317448e8a83d42042079148d%3A5f6c8d37a1f54460a5e8f49ef2c4a9a0%3AFrogmores?action=launch&silent=true" --close-launcher`


*LauncherBridge will automatically snapshot processes, launch Epic, ignore `EpicGamesLauncher.exe`/`EpicWebHelper.exe`, detect `AlanWake2.exe`, and track it to completion.*

---

### 2. EA App
Launch games via EA App protocol URIs or executables.

- **Target**: `"C:\Tools\LauncherBridge.exe"`
- **Launch Options**: `--launch "origin2://game/launch?offerIds=1000001&authCode="`

*(Optional fallback if auto-detect is bypassed)*:
```cmd
LauncherBridge.exe --launch "origin2://game/launch?offerIds=1000001" --process "EASportsFC24"
```

---

### 3. Ubisoft Connect
Launch games via Ubisoft Connect URIs (`uplay://launch/<GameID>/0`).

- **Target**: `"C:\Tools\LauncherBridge.exe"`
- **Launch Options**: `--launch "uplay://launch/5105/0"`

---

### 4. Battle.net
Launch games via Battle.net URIs (`battlenet://`).

- **Target**: `"C:\Tools\LauncherBridge.exe"`
- **Launch Options**: `--launch "battlenet://Fen"`

---

## 🛠️ Building & Publishing

LauncherBridge is built using **.NET 9** and targets Windows. It is compiled as a self-contained, single-file executable with no external dependencies required on the target machine.

### Build & Run Tests
```bash
dotnet build LauncherBridge.sln
dotnet test LauncherBridge.sln
```

### Publish Lightweight Executable (Windows x64 - ~182 KB)
```bash
dotnet publish LauncherBridge/LauncherBridge.csproj \
  -c Release \
  -r win-x64 \
  --self-contained false \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=false
```

### Publish Self-Contained Executable (Windows x64 - ~35 MB)
```bash
dotnet publish LauncherBridge/LauncherBridge.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true
```

The published executable will be generated at:
`LauncherBridge/bin/Release/net9.0/win-x64/publish/LauncherBridge.exe`

---

## 📄 License
MIT License. Free for personal and commercial use.
