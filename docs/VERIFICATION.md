# Verification log

## Shortcut activation modifier-state fix

- Fixed physical key-release events being swallowed during blackout. This could leave Windows/WPF with Ctrl/Alt/Shift logically held when blocking began with the global shortcut. Modifier presses are forwarded only to an overlay; ordinary hidden character presses remain suppressed, and synthetic input is still filtered.
- Release build passed with 0 warnings/errors. All 36 unit tests pass, including activation-key release, balanced reveal modifiers, subsequent password-character routing, and retained blackout/injected-input filtering. Two-display native smoke tests also pass.
- The user confirmed button activation works. The repaired physical hotkey-to-password sequence still requires user verification; automated policy tests model its key events rather than emulating physical hardware.
- The local MSI-installed binaries and InputBlocker-blackout-win-x64.zip were updated. The original MSI is unchanged.

## Requested blackout and reveal behavior

- Blocking now starts with a pure-black cover on every display and no cursor. Ctrl+Shift+Plus (main +/= or keypad +) reveals only the primary password interface; it does not unlock. Other input remains suppressed while concealed. Ctrl+Alt+F12 still performs emergency recovery.
- Release build: 0 warnings/errors. All 24 unit tests pass, including reveal-chord validation. Native smoke tests pass on two displays: rendered pixels are opaque black initially, the primary interface appears on reveal, secondary displays stay black, state remains Blocked, and reactivation starts black again.
- The reveal callback and shortcut recognition were tested separately; physical shortcut/password entry remains a user integration check.
- Installed application binaries are updated directly. The previous MSI remains an older build; the new portable bundle is InputBlocker-blackout-win-x64.zip.

## Overlay rendering repair

- Removed the zero-opacity entrance animation; unlock controls are opaque from the first frame.
- Overlay windows use software rendering. Activation waits up to three seconds for ContentRendered on every display and verifies password focus before installing hooks; failure releases the barrier.
- Release build: zero warnings/errors; all 18 core tests pass. Expanded native smoke test passes on two displays, including first-render readiness, opacity, software rendering, focus, hooks and cleanup.
- Updated the existing local installation and self-contained publish output. The original MSI/ZIP are older; the repaired ZIP is named InputBlocker-overlay-fix-win-x64.zip.
- Physical typing and confirmation that the reported black screen no longer occurs remain to be checked by the user.

Date: 2026-10-29. Build host: Windows x64 (10.0.26200). SDK: .NET 10.0.403 installed locally.

- Release compilation: passed with 0 warnings and 0 errors.
- Automated core tests: 18 passed, 0 failed, 0 skipped.
- Tests cover Unicode passwords, salt uniqueness, incorrect passwords, password changes, short-password rejection, malformed hash rejection, atomic persistence/invalid-settings preservation, shortcut validation, throttling, transition ordering, duplicate activation, failed hooks, partial overlays, recovery during preparation and cleanup errors.
- Native Windows smoke test: passed on this host with **two overlays**, actual keyboard/mouse hook installation, recovery cleanup, reactivation, unlock cleanup and no remaining overlay windows. The isolated harness has a 10-second process deadline. It does not establish physical-input suppression or authenticate a password through the UI.
- Onboarding was launched and visually inspected; a derived-window theme issue was found, corrected and re-inspected successfully. The final installed dashboard was also visually inspected.
- Final per-user MSI installation: exit code 0; installed application launched successfully; SHA-256 of installed InputBlocker.dll matches the final publish output.
- Earlier MSI install/uninstall cycle passed with exit code 0, no installation directory and no startup entry remaining. **Final MSI uninstall did not complete:** Windows Installer stopped making progress at InstallValidate. The test's client was terminated after several minutes; a retry with Restart Manager disabled returned 1618 (another installation in progress). The system Windows Installer service was not terminated. The final test installation remains at `%LOCALAPPDATA%\InputBlockerApp`, closed, with no InputBlocker startup entry. Treat installer lifecycle qualification as unresolved; prefer the portable ZIP until retested. When Windows Installer is available, uninstall product `{0A671CEE-AD32-4D37-B12F-FD6298E8AB7D}` through Windows Settings or `msiexec /x {0A671CEE-AD32-4D37-B12F-FD6298E8AB7D}`. A reboot may be needed to clear the host's pending installer transaction; none was initiated.
- Portable ZIP: 478 entries, and its InputBlocker.dll SHA-256 matches the final publish output. Both distributions include the self-contained runtime and localized resources.
- The verification snapshot inside the packages predates installer lifecycle validation; this repository log contains the later results.
- Physical keyboard/mouse suppression, actual password-entry flow, mixed-DPI coverage correctness, sleep/session changes, crash recovery and Claude workflows: not executed. See MANUAL-TESTS.md.
- Generated artifacts are unsigned. They are working build outputs, not a claim of completed production qualification.
