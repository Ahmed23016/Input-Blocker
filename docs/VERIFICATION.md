# Verification log

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
