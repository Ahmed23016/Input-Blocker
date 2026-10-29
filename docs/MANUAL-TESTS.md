# Windows integration test plan

Record Windows build, app version/hash, display layout/scaling, keyboard layouts, automation versions and results for each run. Keep Task Manager recovery available. These tests are **pending unless recorded in VERIFICATION.md**.

| Test | Procedure and required result |
|---|---|
| First run | Create matching 8+ character password; mismatches/short passwords rejected. Cancel setup exits cleanly. Restart retains configuration. |
| Single monitor | Block from button; overlay covers taskbar/full monitor; dashboard cannot be dismissed to expose desktop through ordinary UI. |
| Multiple monitors | Block on 2–3 monitors, including negative-coordinate layouts. Every display covered; password UI only on primary. |
| Mixed DPI | Repeat at 100/150/200% scaling; no gaps, clipping or unresponsive controls. |
| Keyboard | Type in underlying editor before blocking; after blocking ordinary typing and Alt+Tab/Alt+F4/Windows shortcuts do not edit or switch to it. |
| Mouse | Click, wheel and drag over different displays; no background app interactions. Overlay Unlock works. |
| Unicode input | Test standard, AltGr, dead-key and IME layouts with Unicode passwords. Record unsupported IME/focus paths; recovery must work. |
| Failed authentication | Wrong input clears; after 3 failures delay appears; attempts during delay do not invoke another hash. |
| Unlock | Correct password via Enter and button releases input immediately and closes every overlay. Test while KDF is running and recovery fires. |
| Resolution change | Change resolution during blocking using another process; barrier releases and future activation covers new bounds. |
| Disconnect | Disconnect/reconnect primary/secondary monitor; blocking releases; reactivation has exactly one unlock display. |
| Crash | End process while blocked; hooks vanish and desktop works. Relaunch does not auto-block immediately. |
| UI hang | In a debugger pause UI thread while hooks run; after 15–20 seconds the process terminates. Do not pause all threads for this test. |
| Sleep/resume | Sleep/resume while blocked; recovery releases overlays/hooks and auto-block waits its configured duration. |
| Session change | Windows lock/unlock and switch user; blocker releases its barrier. No credential UI modification. |
| Security recovery | Ctrl+Alt+F12 releases without password. Ctrl+Alt+Delete stays available; Task Manager can terminate process. |
| Foreground loss | Bring a privileged/other app forward; barrier releases within the one-second health tick rather than fighting focus. |
| Claude task | Run an identifiable long CLI/network task; record progress before/during/after blocking and compare output. Repeat with Claude Code, browser Claude, and computer-use agent separately. Do not infer one result from another. |
| Synthetic input | Use a dedicated test app with SendInput. Confirm flagged input is suppressed. Test browser protocol/UI Automation separately; record bypass and focus limitations. |
| Shortcut conflicts | Register chosen shortcut in another test program; settings reports conflict and restores the old shortcut. Recovery-shortcut conflict must prevent startup. |
| Rapid activation | Repeated button/hotkey/tray actions create only one set of overlays/hooks. Repeat during countdown and password verification. |
| Auto-block | Test 1/5/10/15/30-minute values, active input resets idle, and recovery avoids immediate repeated activation. |
| Tray/startup | Minimize, close, restore, exit; verify startup opt-in runs from correct path. No settings changes on mere Cancel. |
| Themes/accessibility | Light/dark/system, high contrast, keyboard navigation, screen-reader labels, reduced motion, small display/resolution. |
| Installer | Fresh per-user MSI install, launch, startup toggle, repair, upgrade and uninstall. No startup entry remains after uninstall. Settings retention is intentional. |
| Performance | Record startup time, working set and idle/blocked CPU for 10 minutes; no busy loops or repeated focus theft. |

Fault injection in automated core tests covers failed hook installation and partial overlay preparation. Actual native API failure and silent hook timeout removal still need Windows diagnostic testing; Windows supplies no reliable removal notification.
