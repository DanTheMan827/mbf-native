# Revision v7

This revision continues the native WinUI port with the following behavior changes:

- every MBF Rust-agent invocation is surfaced through a blocking, application-level live-log overlay;
- Mods and Tools stay disabled until a ready device has been verified as Scotland2-modded;
- losing that verified state while on Mods/Tools returns the shell to Home;
- a newly verified modded installation automatically activates the Mods page;
- a single detected ADB device is selected and connected automatically;
- Connect and Disconnect command availability mirrors the current connection state;
- Patch Options disappear after the installation is verified as patched;
- Mods uses separate **Your Mods** and **Install Mods** tabs;
- Your Mods sorts updates first, then non-core mods, then core mods;
- Install Mods keeps updates first and uses web-inspired cards with cover art, selection above the action button, and Source/Report bug actions below the description;
- remote mod cover URLs are converted to native WinUI `BitmapImage` instances in the presentation project;
- Home/Mods/Tools/Logs page shells use consistent responsive stretching and spacing;
- warning `InfoBar`s use native `Message` content and non-closable layout to avoid cramped custom-content padding;
- catalog regression coverage now verifies update ordering, cover propagation, global-mod filtering, and core-update suppression.
- Core agent-JSON tests now validate parsed JSON values instead of relying on `System.Text.Json`'s textual escaping of XML (`<`/`>` may be emitted as `\\u003C`/`\\u003E`); this fixes the Release CI failure while keeping the Rust wire contract strict.

The source tree was statically validated for XAML/XML well-formedness, XAML code-behind/event-handler references, project-layer UI dependency boundaries, and known WinUI 2/WPF-control regressions. A full Windows compiler/test run still requires a Windows machine with the .NET 10 SDK and Windows App SDK toolchain.
