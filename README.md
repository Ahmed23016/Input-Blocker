# Input Blocker

A native C# / .NET 10 / WPF desktop utility for Windows 10 and Windows 11 x64. Cover every display and suppress ordinary keyboard/mouse input while your Windows session stays signed in. Background computations, downloads and network activity are not intentionally paused.

**This is a casual deterrent, not a security boundary or a replacement for Windows Lock.** Anyone who knows the recovery shortcut can release it. Administrators, other processes running as you, Windows security screens, touch/pen paths, accessibility tools and OS recovery facilities are outside its protection guarantees.

## Install and run

Release output is generated in `artifacts/`:

- `InputBlocker-1.0.0-x64.msi`: per-user installation, Start menu shortcut and standard Windows uninstall registration.
- `InputBlocker-1.0.0-win-x64.zip`: extract the **whole folder**, then run `InputBlocker.exe`. No separate .NET runtime is required.

The builds are unsigned. Code signing and validation on the supported Windows versions are required before distributing a trusted production release. Windows may show an unknown-publisher prompt.

**Current validation caveat:** prefer the portable ZIP. MSI installation and launch passed, but the final uninstall test stalled in Windows Installer validation and its retry returned 1618. See `docs/VERIFICATION.md` for the exact state and cleanup instructions. Installer lifecycle qualification remains unresolved.

On first launch, create and confirm a password with at least eight Unicode characters, then acknowledge the recovery instructions. The dashboard provides Block Input, password changes, shortcut settings and inactivity blocking. Settings also offer startup, minimize/close to tray, notifications, dark/light/system themes, reduced motion and a cancellable three-second countdown. Password reveal is available during setup/change, not on the overlay.

## Everyday use

Click **Block Input**, use the tray menu, or press **Ctrl + Alt + Shift + L**. Each monitor receives a borderless topmost overlay. Enter your password on the primary display and press Enter or click Unlock. Incorrect attempts clear the field. After three failures, retry delays grow up to 30 seconds. Password hashing runs off the UI thread.

Change Password requires the current password by default. Turning this requirement off explicitly permits anyone at an unlocked dashboard to replace it. Global shortcuts can use Ctrl+Alt with A–Z and optional Shift; registration conflicts are reported. The fixed recovery shortcut must be available at startup or the app refuses to start.

Start with Windows is opt-in and uses only the current user's Run registry key. Do not move a portable installation after enabling startup; disable/re-enable startup after moving it. Closing/minimizing to the tray is configurable; Exit in the tray releases resources and terminates the app.

## Recovery

1. Press **Ctrl + Alt + F12** to immediately release the barrier without a password.
2. If the app is unresponsive, press **Ctrl + Alt + Delete**, open Task Manager, and end `InputBlocker.exe`. Windows removes process-owned hooks when the process exits. Signing out or restarting also recovers input.
3. Display topology/resolution changes, power/session changes and loss of overlay foreground focus deliberately release blocking. Re-activate after the change.
4. If the UI stops servicing its one-second heartbeat for over 15 seconds during blocking, a separate timer terminates the app to release input. This is a termination safeguard, not a relaunch watchdog. It cannot guarantee recovery from an OS-wide hang.

There is no password-independent *security* claim: recovery deliberately bypasses the password. Forgotten passwords can be reset by exiting and renaming `%LOCALAPPDATA%\InputBlocker\settings.json`, then restarting onboarding. Do not edit settings while the app is running. Invalid or corrupt settings stop startup with an error; they are not silently overwritten.

## Compatibility and limitations

- Background work and interactive desktop automation are different. CLI agents and server-driven browser automation may continue; this must be tested for the actual workflow.
- Keyboard and mouse hook flags identify some injected events but cannot authenticate the generating application. This version **suppresses flagged injected input**; it offers no arbitrary synthetic-input bypass or unsafe application allowlist.
- Claude computer use, SendInput-based agents, remote control and accessibility input can fail while blocked. The overlay also obscures screenshots and mouse targets. UI Automation and browser protocol calls can behave differently; no universal compatibility is claimed.
- Password input uses the native WPF PasswordBox and Windows layout handling, without custom key-to-character conversion. IMEs, AltGr/dead keys, touch keyboards and accessibility input need manual verification; external IME windows may cause safe release or be inaccessible. Use recovery if the password cannot be entered.
- Windows secure attention and protected desktops are never intercepted. Task Manager is not disabled. Ctrl+Shift+Esc is allowed; foreground loss then releases the barrier.
- Low-level hooks cannot provide an absolute guarantee. Windows can **silently remove a hook on timeout without a supported query or notification**. Callbacks are short and run on a dedicated message-pump thread, and overlay/focus/heartbeat safeguards reduce exposure; silent removal is not claimed to be reliably detected. No high-frequency synthetic probe is injected into other apps.
- Topmost overlays are not exclusive fullscreen and cannot control higher-integrity windows or other privileged surfaces. Touch, pen, driver-level input and hostile same-user applications are not comprehensively blocked.
- No sleep-prevention request is made: Windows power policies can still sleep or lock the session. Input Blocker itself never calls session locking, suspend, logoff or disconnect APIs.

Relevant Microsoft API references: [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc), [MSLLHOOKSTRUCT injection flags](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msllhookstruct), [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey).

## Storage and security design

Settings are stored atomically in `%LOCALAPPDATA%\InputBlocker\settings.json`. Its directory grants access to the current user, SYSTEM and administrators. Password records contain a 32-byte random salt, a 32-byte PBKDF2-HMAC-SHA256 hash and a documented work factor of **600,000 iterations**. Verification uses fixed-time comparison. Plaintext passwords are never persisted or logged; temporary UTF-8 buffers are zeroed. Managed strings used for WPF input cannot be guaranteed to be erased from memory.

No telemetry or network access is required for normal application operation. The installer and build tools may download dependencies when building. Configuration is not encrypted: same-user processes and administrators can modify or replace it. The named per-user session mutex prevents normal conflicting launches, not malicious interference.

## Architecture

- `InputBlocker.Core`: validated configuration, authentication, throttling, persistence and testable blocking state machine with injected barrier operations.
- `InputBlocker`: WPF views, dashboard view model/commands, dedicated-thread hooks, overlay lifecycle, global shortcuts, system tray and OS lifecycle integration. Native declarations are isolated in `Infrastructure/NativeMethods.cs`.
- `tests/InputBlocker.Tests`: automated desktop-independent tests, including partial initialization, hook failure and recovery ordering.
- `installer`: reproducible WiX MSI generation from the self-contained publish directory.

Activation transitions Unlocked → PreparingToBlock → Blocked, with all overlays shown and checked before hook installation. Unlock transitions through Unlocking and releases hooks before closing overlays. Errors transition through Recovering; cleanup failures produce Error and the Windows host terminates rather than retaining an uncertain barrier. Native hooks are also removed by their owning thread's `finally` block.

The dashboard uses MVVM; password controls keep password strings out of bindable view-model properties. Small window classes own UI interaction code. Settings edit a draft and apply validated values together, rolling back shortcut/startup changes on persistence failure.

## Build, test and package

Prerequisites: Windows x64 and .NET 10 SDK. `global.json` accepts current .NET 10 feature bands. No administrator access is required.

```powershell
dotnet restore InputBlocker.sln
dotnet build InputBlocker.sln -c Release
dotnet test tests/InputBlocker.Tests -c Release
./build.ps1
```

To choose a locally installed SDK:

```powershell
./build.ps1 -DotNet "$env:LOCALAPPDATA/InputBlockerBuild/dotnet/dotnet.exe"
```

The script builds, tests, publishes self-contained `win-x64`, creates a ZIP, and builds the MSI using pinned WiX 6.0.2. Use `-SkipInstaller` for portable output only. WiX is installed locally under ignored `.tools/wix`; NuGet access is needed on first build. The executable icon is embedded. Release artifacts remain ignored by Git.

For an optional real-desktop smoke test, run `dotnet run --project tests/InputBlocker.SmokeTests -c Release` or pass `-NativeSmoke` to the build script. It briefly covers all displays and installs real hooks, then verifies recovery/reactivation/unlock cleanup. A separate ten-second deadline terminates a stalled test. Do not run it during sensitive desktop interactions. It uses no saved application password or user settings.

The MSI reserves an empty startup registry value so Windows Installer owns its uninstall cleanup; an empty value does not launch anything. The application reconciles that value to the saved opt-in preference when started.

## Uninstall

Exit from the tray first. For MSI installations, uninstall Input Blocker in Windows Settings → Apps. The MSI removes its startup registry entry. Windows Installer can request closing running files; do not leave the app running during removal.

For portable installs, turn off Start with Windows, exit the app, and delete the extracted folder. If files were already removed, remove the `InputBlocker` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

User settings are retained for reinstall. To remove them, delete `%LOCALAPPDATA%\InputBlocker` after exiting. Hooks are process resources; there are no drivers, services, device disabling, cursor confinement or persistent input hooks to uninstall.

## Verification status

See [manual integration checklist](docs/MANUAL-TESTS.md) and [build verification](docs/VERIFICATION.md). Passing unit tests and compiling the MSI do not establish physical desktop input safety or production readiness. Full Windows 10/11 hardware and automation compatibility validation remains required.
