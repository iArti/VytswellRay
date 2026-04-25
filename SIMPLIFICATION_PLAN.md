# VytswellRay — Simplification Plan

**Goal:** Fork v2RayN into a minimal workplace proxy client with a 3-button UI, hardcoded security policy, and the smallest possible technical surface.

**Scope guardrails:**
- Windows-only WPF (we delete `v2rayN.Desktop` entirely).
- Only **sing-box** core supported — no Xray, v2fly, hysteria, mihomo, clash, etc.
- Only **TUN mode** — no HTTP/SOCKS system-proxy mode, no PAC.
- Routing policy is **hardcoded** (bypass-LAN / block-ads / proxy-rest). Users cannot edit it.
- **Local-network bypass is hardcoded** (private IP ranges, mDNS, link-local, RFC1918 — never proxied).
- No subscriptions, no profile groups, no proxy chains, no policy groups, no WebDAV, no Clash API, no statistics UI, no speed-test, no backup.
- **Localization:** Russian (default) + English (fallback). Switchable via globe icon on main window.
- **Hotkey:** single global shortcut for Start/Stop service (configurable in Settings).
- 3 user-visible buttons: **Connect**, **Connections**, **Settings**.

---

## 1. Target UX

### Main window (FluentWindow with Mica)

```
+---------------------------------------------+
|  VytswellRay                  🌐  _  □  ×   |  ← globe icon = language switch (RU/EN)
|                                             |
|                                             |
|          ┌─────────────────────┐            |
|          │                     │            |
|          │     ПОДКЛЮЧИТЬ      │   ← large, pill button, becomes
|          │                     │     "ОТКЛЮЧИТЬ" when active
|          └─────────────────────┘            |
|                                             |
|          Статус: Не подключено              |
|          Активно: <имя профиля или "Нет">   |
|                                             |
|                                             |
|   [ Подключения ]      [ Настройки ]        |  ← bottom row, equal size
|                                             |
+---------------------------------------------+
```

- **Connect** — primary `ui:Button` with `Appearance="Primary"`, ~200×64 px. Disabled if no active connection. Shows spinner during start/stop.
- **Globe icon** (top-right of `ui:TitleBar`, before window controls) — toggles RU↔EN; choice persisted in config; whole UI re-binds via `LocalizedResources` instance.
- **Connections** — opens modal `ContentDialog` listing profiles, with "Paste connection URL" input and per-row delete / set-active.
- **Settings** — opens modal `ContentDialog` with: Start with Windows toggle, theme (System/Light/Dark), Start/Stop hotkey capture box, version info, "Open log folder" button, geosite "Last updated: …" + manual "Update now" button.
- **System tray** icon (`WPF-UI.Tray.NotifyIcon`): **two menu items only** — `Запустить`/`Остановить` (toggles based on state) and `Выход`. Left-click shows window.
- App **requires admin** (TUN needs driver access — already declared in `app.manifest`).

### Status rules
- `Disconnected` — grey dot, Connect button says "Подключить" / "Connect".
- `Connecting` — amber dot, button disabled with ProgressRing.
- `Connected` — green dot, button says "Отключить" / "Disconnect".
- `Error` — red dot, `InfoBar` below status with message (localized — sing-box raw error appended in monospace).

---

## 2. Architecture overview

### What we keep from `ServiceLib`
| Keep | Reason |
|---|---|
| `Models/ProfileItem`, `ConfigItems`, `SubItem` (light use) | Core data shape |
| `Manager/AppManager` (simplified) | DI entry, config init |
| `Manager/CoreManager` | Starts/stops sing-box |
| `Manager/CoreInfoManager` | Knows where `sing-box.exe` lives |
| `Handler/ConfigHandler` | Profile CRUD + SQLite |
| `Handler/CoreConfigHandler` | Build sing-box JSON (sing-box path only) |
| `Handler/Fmt/FmtHandler` + protocol parsers | Parse pasted URLs |
| `Services/CoreConfig/Singbox/*` | Build config for sing-box |
| `Services/DownloadService` | For updates, optional |
| `Services/ProcessService` | Launch sing-box |
| SQLite persistence | Stores profiles |

### What we delete / neutralize
| Remove | Note |
|---|---|
| **Entire `v2rayN.Desktop` project** | Linux/macOS Avalonia client |
| **Entire `ServiceLib.Tests`** | Unless you want them |
| **`AmazTool`** | Self-updater — skip for now, add later if needed |
| ~~`GlobalHotKeys` submodule~~ **Keep** | One global Start/Stop hotkey — `v2rayN/Manager/HotkeyManager.cs` already wires it |
| All `Services/CoreConfig/V2ray/*` | Xray unsupported |
| `Services/CoreConfig/CoreConfigClashService.cs` | Clash unsupported |
| `Manager/ClashApiManager`, `PacManager`, `WebDavManager`, `GroupProfileManager`, `StatisticsManager`, `CoreAdminManager`, `CertPemManager` | Features dropped |
| `Handler/SubscriptionHandler` | No subscriptions |
| `Handler/SysProxy/*` | No system-proxy mode — TUN only |
| `Handler/Builder/*` (V2ray & Clash builders) | Only sing-box builder stays |
| `Handler/Fmt/ClashFmt.cs`, `V2rayFmt.cs`, `HtmlPageFmt.cs` | Non-URL import paths |
| All ViewModels except `MainWindowViewModel` (rewritten) + 2 new ones | Replaced |
| All Views except rewritten `MainWindow` + 2 dialogs | Replaced |

### What gets rewritten
- `App.xaml` / `App.xaml.cs` — pull in WPF-UI resource dictionaries, set initial theme.
- `MainWindow.xaml` — new 3-button layout using WPF-UI controls.
- `MainWindowViewModel` — cut from ~1000+ lines of reactive state to ~150 lines: IsConnected, ActiveProfile, Connect/Disconnect commands.
- New `ConnectionsDialogViewModel` — list + paste-URL add + delete + set-active.
- New `SettingsDialogViewModel` — toggles only.

---

## 3. Hardcoded security policy

The routing + DNS + inbound sections of the sing-box config are **not user-editable**. They are generated from constants in a new file `ServiceLib/Services/CoreConfig/Singbox/FixedPolicy.cs`.

### 3.1 TUN inbound (from existing `tun_singbox_inbound` template)
```json
{
  "type": "tun",
  "tag": "tun-in",
  "interface_name": "vytswell_tun",
  "address": ["172.19.0.1/30", "fdfe:dcba:9876::1/126"],
  "mtu": 9000,
  "auto_route": true,
  "strict_route": false,           // NO kill-switch — traffic falls through if sing-box dies
  "stack": "system",
  "sniff": true
}
```
`strict_route: false` means if sing-box exits/crashes, the TUN adapter is removed and traffic continues raw. **Trade-off:** brief leak window during process death, but app feels less "fragile" to users on flaky networks.

### 3.2 Outbounds (always 3)
```json
[
  { "type": "<proto>", "tag": "proxy", ... },   // built from active ProfileItem
  { "type": "direct", "tag": "direct" },
  { "type": "block",  "tag": "block" }          // sing-box uses `type: block` (legacy) OR route action `reject`
]
```
Note: modern sing-box (≥ 1.11) prefers `"action": "reject"` rule fields over a `block` outbound — we'll use the rule-action form.

### 3.3 Routing rules — translated from user-defined policy to sing-box format

Source rules (v2rayN/Xray format):
```
Российские сайты  : domain:ru, vk.com          → direct
Торрент напрямую  : protocol=bittorrent          → direct
Блокировка рекламы: geosite:category-ads-all     → block
Приватные сети    : geoip:private                → direct
Приватные домены  : geosite:private              → direct
Только в России   : geoip:ru                     → direct
Остальное         : 0-65535 (catch-all)          → proxy
```

Translated to **sing-box `route.rules`** (top-to-bottom):

```json
"rules": [
  {
    "protocol": "dns",
    "action": "hijack-dns"
  },
  {
    "protocol": "bittorrent",
    "outbound": "direct"
  },
  {
    "rule_set": ["geosite-ads"],
    "action": "reject"
  },
  {
    "ip_is_private": true,
    "outbound": "direct"
  },
  {
    "rule_set": ["geosite-private"],
    "outbound": "direct"
  },
  {
    "rule_set": ["geoip-ru"],
    "outbound": "direct"
  },
  {
    "domain_suffix": [".ru", "vk.com"],
    "outbound": "direct"
  }
]
```
```json
"final": "proxy"
```

**Translation notes:**
- `domain:ru` (Xray) = suffix match for all `.ru` TLD → `domain_suffix: [".ru"]`
- `vk.com` (bare domain) = vk.com and all subdomains → `domain_suffix: ["vk.com"]` (combined with `.ru` above)
- `protocol=bittorrent` → `"protocol": "bittorrent"` — works because `sniff: true` is set on the TUN inbound
- `geoip:private` → `"ip_is_private": true` (sing-box built-in helper, same address set)
- `port: 0-65535` (catch-all) → becomes `"final": "proxy"` (sing-box uses a separate field for the default action)
- `geosite:category-ads-all`, `geosite:private`, `geoip:ru` → remote rule_sets (see 3.3.1)

**LAN belt-and-suspenders:** `ip_is_private: true` covers all RFC1918 / loopback / link-local. That's sufficient; no need for an extra static CIDR list.

### 3.3.1 Rule-sets — auto-updated weekly via sing-box native mechanism

Use sing-box's built-in `"type": "remote"` rule sets with `update_interval: "168h"`. **No custom downloader code needed.** sing-box caches `.srs` on disk, retries on failure, and uses cached version offline.

```json
"rule_set": [
  {
    "type": "remote",
    "tag": "geosite-ads",
    "format": "binary",
    "url": "https://raw.githubusercontent.com/SagerNet/sing-geosite/rule-set/geosite-category-ads-all.srs",
    "download_detour": "direct",
    "update_interval": "168h"
  },
  {
    "type": "remote",
    "tag": "geosite-private",
    "format": "binary",
    "url": "https://raw.githubusercontent.com/SagerNet/sing-geosite/rule-set/geosite-private.srs",
    "download_detour": "direct",
    "update_interval": "168h"
  },
  {
    "type": "remote",
    "tag": "geoip-ru",
    "format": "binary",
    "url": "https://raw.githubusercontent.com/SagerNet/sing-geobox/rule-set/geoip-ru.srs",
    "download_detour": "direct",
    "update_interval": "168h"
  }
]
```

**`download_detour: "direct"`** — fetches over raw connection, not the proxy. Updates work even when proxy is misconfigured. If `raw.githubusercontent.com` is unreachable from your network, update the URL constants in `FixedPolicy.cs` to a mirror.

**Bootstrap (first-run, offline):** Ship seed copies of all three `.srs` files in `Resources/rule_sets/`. On first start, copy them to sing-box's cache directory before starting the core. Subsequent updates overwrite via sing-box's mechanism.

**Manual override** in Settings: "Обновить списки сейчас" button — stop + start core (sing-box checks `update_interval` on start and fetches if expired).

**Last-updated display:** read mtime of sing-box's cached `.srs` files and show in Settings ("Обновлено: 2 дня назад").

### 3.4 DNS
Fixed two-tier resolver (no user knobs):
- **proxy resolver** — `tls://1.1.1.1` via proxy outbound (for proxied traffic)
- **direct resolver** — `https://dns.google/dns-query` via direct outbound (for bypassed traffic)
- **fakeip** disabled (simpler, fewer corner cases on a work LAN)

### 3.5 Log
```json
{ "level": "warn", "output": "%LOCALAPPDATA%/VytswellRay/sing-box.log", "timestamp": true }
```

All the above lives in one static class that returns a `SingboxConfig` object with the user's `ProfileItem` slotted into the proxy outbound. **No JSON template strings in user-editable locations.**

---

## 4. Implementation phases

### Phase 0 — Branch & baseline (0.5 h)
- `git checkout -b simplify-workplace-fork`
- Delete `.github/workflows/build-linux.yml`, `build-osx.yml`, `build-windows-desktop.yml`, `build-all.yml` (keep `build-windows.yml` + `build.yml` trimmed to Windows only).
- Delete `README.md`, write a 1-screen internal README.

### Phase 1 — Solution cleanup (1 h)
1. Open `v2rayN/v2rayN.sln` in Rider/VS.
2. Remove projects from solution & delete folders:
   - `v2rayN.Desktop/`
   - `AmazTool/`
   - `ServiceLib.Tests/` (optional — keep if you want tests)
   - `GlobalHotKeys/` (also drop submodule from `.gitmodules`)
3. In `v2rayN/v2rayN.csproj`, remove any `ProjectReference` to the removed projects.
4. `dotnet build v2rayN.sln` — expect it to still succeed (ServiceLib + v2rayN only).

### Phase 2 — Package swap (0.5 h)
In `Directory.Packages.props`:
- **Remove:** `MaterialDesignThemes`, `H.NotifyIcon.Wpf`, `ReactiveUI.Fody` (we'll stop using Fody — optional, but makes project leaner), all `Avalonia.*`, `Semi.Avalonia.*`, `DialogHost.Avalonia`, `AwesomeAssertions`, `Microsoft.NET.Test.Sdk`, `xunit.*`.
- **Add:**
  - `WPF-UI` 4.2.1
  - `WPF-UI.Tray` 4.2.0

Keep: `ReactiveUI`, `ReactiveUI.WPF`, `NLog`, `sqlite-net-pcl`, `CliWrap`, `Downloader`, `QRCoder` (for "share connection QR" inside Connections dialog — optional, can drop), `YamlDotNet`, `TaskScheduler`, `ZXing.Net.Bindings.SkiaSharp` (only if you keep QR scan from camera — probably drop).

`dotnet restore v2rayN.sln` must succeed.

### Phase 3 — Strip ServiceLib (2–3 h)
In `v2rayN/ServiceLib/`:
1. Delete folders: `Services/CoreConfig/V2ray/`, `Handler/Builder/V2ray/`, `Handler/Builder/Clash/`, `Handler/SysProxy/`.
2. Delete files: `Manager/ClashApiManager.cs`, `Manager/PacManager.cs`, `Manager/WebDavManager.cs`, `Manager/GroupProfileManager.cs`, `Manager/StatisticsManager.cs`, `Manager/CoreAdminManager.cs`, `Manager/CertPemManager.cs`, `Handler/SubscriptionHandler.cs`, `Services/CoreConfig/CoreConfigClashService.cs`, `Handler/Fmt/ClashFmt.cs`, `Handler/Fmt/V2rayFmt.cs`, `Handler/Fmt/HtmlPageFmt.cs`.
3. Delete Sample templates we don't need: `custom_routing_*`, `clash_*`, `pac`, `proxy_set_*`, `linux_autostart_config`, `dns_v2ray_normal`, all `SampleHttpRequest/Response/Inbound/Outbound` except sing-box variants.
4. In `ServiceLib/Enums/ECoreType.cs`, remove all values except `sing_box`. Anywhere this breaks a switch, prune the branch.
5. In `ServiceLib/Manager/CoreInfoManager.cs`, keep only sing-box info; delete Xray/Hysteria/etc entries.
6. In `ServiceLib/Handler/CoreConfigHandler.cs`, delete all non-sing-box code paths.
7. In `ServiceLib/Manager/AppManager.cs.InitApp()`, remove calls to subscription / PAC / stats / WebDAV / Clash init.
8. In `ServiceLib/Handler/ConfigHandler.cs` (2,654 lines), aggressively delete methods: `AddCustomServer4Sub*`, `GroupProfile*`, `MoveToGroup*`, `SubSetting*`, `Routing*` (we hardcode), `DNS*` (we hardcode), `ImportOldGuiConfig` (if you don't need migration), `Backup*`, `Restore*`. Keep: `InitConfig`, `SaveConfig`, `AddServerCommon`, `RemoveServer`, `SetDefaultServerIndex`/`GetDefaultServer`, `CopyServer` (optional), `BatchAddServers` (accepts pasted URL).
9. Build frequently: `dotnet build` in a tight loop. The compiler points at every dangling reference — delete matching dead code.
10. Acceptance: `ServiceLib.csproj` builds clean. If a `ConfigItem` has 80 properties and only 12 are used, leave the unused ones — renaming/removing config schema is risky (SQLite table already exists on user machines).

### Phase 4 — Enforce policy code path (2 h)
Create `ServiceLib/Services/CoreConfig/Singbox/FixedPolicy.cs`:
- `public static async Task<string> BuildConfigAsync(ProfileItem profile)` returns the final JSON string.
- Internally calls the existing `SingboxOutboundService` to translate `ProfileItem` → outbound JSON (reuse, it's well-tested).
- Injects hardcoded inbound (`tun_singbox_inbound` template), DNS, routing (above), log block.
- Ignores any user-supplied routing/DNS config entirely.

Modify `CoreConfigHandler.GenerateClientConfigAsync` so the **only** path is `FixedPolicy.BuildConfigAsync(profile)`. Delete the branching for routing modes, custom templates, fakeip toggles, etc.

Drop three seed rule-set files into `v2rayN/v2rayN/Resources/rule_sets/`:
- `geosite-category-ads-all.srs`  — ad blocking
- `geosite-private.srs`           — private domains bypass
- `geoip-ru.srs`                  — Russian IPs bypass (~5 MB, largest file)

Set `<Content Include="Resources\rule_sets\*.srs"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>` in `v2rayN.csproj`. Reference them as `file://%APPDIR%/rule_sets/...srs` in the generated config (or better: `"type": "local"` with a resolved absolute path at build-time).

### Phase 5 — WPF UI rebuild (3–4 h)
1. Delete every file under `v2rayN/v2rayN/Views/` **except** `MainWindow.xaml[.cs]`. Delete every file under `v2rayN/v2rayN/ViewModels/` except `MainWindowViewModel.cs`. Delete unused converters in `v2rayN/v2rayN/Converters/`.
2. Update `App.xaml`:
   ```xml
   <Application ...
                xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
     <Application.Resources>
       <ResourceDictionary>
         <ResourceDictionary.MergedDictionaries>
           <ui:ThemesDictionary Theme="Dark" />
           <ui:ControlsDictionary />
         </ResourceDictionary.MergedDictionaries>
       </ResourceDictionary>
     </Application.Resources>
   </Application>
   ```
3. In `App.xaml.cs.OnStartup`, after DI init:
   ```csharp
   Wpf.Ui.Appearance.SystemThemeWatcher.Watch(Application.Current.MainWindow);
   ```
4. Rewrite `MainWindow.xaml` as `<ui:FluentWindow>` with:
   - `ui:TitleBar` for custom chrome.
   - Centered `StackPanel` with a large `ui:Button` (primary) + status label.
   - Bottom `Grid` with two equal columns holding `Connections` and `Settings` buttons.
   - `tray:NotifyIcon` (from `WPF-UI.Tray`) with menu items bound to commands.
5. `MainWindowViewModel` surface (target ~150 lines):
   ```csharp
   ConnectionState State { get; }             // Disconnected | Connecting | Connected | Error
   string? ActiveProfileName { get; }
   string? LastError { get; }
   ReactiveCommand ToggleConnectCommand { get; }
   ReactiveCommand ShowConnectionsCommand { get; }
   ReactiveCommand ShowSettingsCommand { get; }
   ReactiveCommand QuitCommand { get; }
   ```
   - `ToggleConnectCommand` reads active profile → calls `CoreManager.StartCoreAsync(profile)` (via `FixedPolicy`) or `CoreManager.CoreStopAsync()`.
   - State transitions emit to `State` for the View to rebind.

6. Create `Views/ConnectionsDialog.xaml` — `ui:ContentDialog` hosting:
   - `ListView` of profiles (columns: name, protocol, address, Delete button, Set-active radio).
   - `TextBox` + "Add" button: calls `FmtHandler.ParseClipboardOrUrl(text)` → `ConfigHandler.AddServerCommon(item)`.
   - Validation: show `ui:InfoBar` error if parse fails.
7. Create `Views/SettingsDialog.xaml` — `ui:ContentDialog` with:
   - `ui:ToggleSwitch` "Запускать с Windows" (bind to `AutoStartupHandler.Set/Get`).
   - `ui:ToggleSwitch` "Автоподключение при запуске" — when on, `MainWindowViewModel` calls `ToggleConnectCommand` after init if an active profile exists.
   - `ComboBox` Тема: Система / Светлая / Тёмная (calls `ApplicationThemeManager.Apply(...)`).
   - **Hotkey row:** `ui:ToggleSwitch` "Использовать горячую клавишу" + capture box (TBD default — picked later by user). Disabled state grays out the capture box. Uses `HotkeyManager.cs` (kept from original); persists to config.
   - **"Обновлено: N дней назад"** label + `ui:Button` "Обновить списки сейчас" (forces sing-box rule-set refresh by stop+start of core).
   - **`ui:Button` "Экспортировать логи"** — opens save-file dialog, zips `%LOCALAPPDATA%/VytswellRay/logs/*` + last sing-box log into `vytswellray-logs-yyyymmdd-hhmmss.zip`. No telemetry, no auto-send.
   - Static version label from assembly.
   - Hyperlink button "Открыть папку логов" — `Process.Start("explorer.exe", logPath)`.

8. **Localization (Russian default, English fallback):**
   - Create `v2rayN/v2rayN/Resources/Strings/Strings.resx` (default = Russian) and `Strings.en.resx` (English fallback).
   - Add `<NeutralLanguage>ru-RU</NeutralLanguage>` to `v2rayN.csproj`.
   - All UI text bound via `{x:Static r:Strings.Connect}` etc.
   - Globe icon in `ui:TitleBar.Header`: clicking flips `Thread.CurrentThread.CurrentUICulture` between `ru-RU` and `en-US`, persists choice in config, raises a `LanguageChanged` event that the main VM listens to and forces a re-bind of all string properties (simplest: re-instantiate the VM from DI).
   - sing-box stderr/log output stays in English (untranslatable third-party output) — display verbatim in error InfoBar.

### Phase 6 — Wire elevation, single-instance, tray, hotkey, TUN cleanup (2 h)
- `app.manifest` already requests `requireAdministrator` — verify still present.
- `App.xaml.cs.OnStartup`: keep existing `EventWaitHandle` single-instance logic; on duplicate start, show main window via `NamedPipe` (the existing `ProgramStarted` event should do this — wire `.Set()` handler to `MainWindow.Show()`).
- **Orphaned TUN adapter cleanup on startup:** before any sing-box launch, run `netsh interface show interface name=vytswell_tun` — if it exists in a stale state (admin=disabled or driver missing), call `netsh interface set interface name=vytswell_tun admin=disabled` then continue. Wrap in try/catch — failure here is non-fatal, sing-box will report a clearer error if the adapter conflicts. Mitigates the `strict_route: false` hard-crash leftover scenario.
- Tray: on window `Closing`, `e.Cancel = true; Hide();` so the app lives in the tray.
- **Tray menu = exactly 2 items** bound to commands:
  - `MenuItem` text bound to `IsConnected ? "Остановить" : "Запустить"` → `ToggleConnectCommand`
  - `MenuItem` "Выход" → `QuitCommand`
- **Hotkey wiring:** keep `v2rayN/Manager/HotkeyManager.cs`. Read enabled-flag + binding from config; if enabled, register → calls `MainWindowViewModel.ToggleConnectCommand.Execute()`. On Settings dialog change, unregister and re-register. If `RegisterHotKey` fails (binding taken by another app), show non-blocking InfoBar "Не удалось зарегистрировать горячую клавишу" — don't crash.
- **Auto-connect on startup:** after `InitApp` succeeds, if `Config.AutoConnectOnLaunch == true` and an active profile exists, dispatch `ToggleConnectCommand.Execute()` after main window load.
- On Quit, call `CoreManager.CoreStopAsync().Wait(3000)` then `Environment.Exit(0)`.

### Phase 7 — sing-box binary distribution (0.5 h)
- Create `v2rayN/v2rayN/Resources/bin/sing-box/` and drop `sing-box.exe` + `wintun.dll` there.
- In `CoreInfoManager`, force `CoreInfo.Path = Path.Combine(Utils.StartupPath(), "Resources", "bin", "sing-box", "sing-box.exe")` for the only supported core.
- `csproj` content-includes as above.
- Decide: ship a pinned version (simplest, recommend) vs. runtime-download. For a work tool, **pin the version** — fewer moving parts.

### Phase 8 — Smoke test matrix (1 h, manual)
| Case | Expected |
|---|---|
| Start app as non-admin | UAC prompt, then runs |
| No profiles added, click Connect | Button disabled |
| Paste valid `vless://` URL | Appears in list; set-active available |
| Paste garbage | InfoBar error, nothing saved |
| Connect with active profile | State→Connecting→Connected; traffic flows through TUN |
| Kill `sing-box.exe` externally | State→Error within ~2 s, InfoBar shown |
| Close window (X) | Window hides, tray icon remains |
| Right-click tray → Quit | sing-box stops, process exits cleanly |
| Disconnect, verify traffic leaks | Should be direct; no TUN interface |
| Connect, unplug network | `strict_route: true` → no internet (kill switch works) |
| Reboot with "Start with Windows" on | Tray appears, not connected unless "auto-connect" (we didn't build that — add later if needed) |

### Phase 9 — Polish (1–2 h, optional)
- App icon + tray icon (replace `v2rayN.ico` with your workplace asset).
- Version bump in `Directory.Build.props`: change `Version` to `1.0.0-vyts` and `AssemblyName`/`RootNamespace` if you want a distinct binary name.
- **Distribution v1:** zip archive of the publish output (x64 + arm64 folders). Users extract and run directly — no installer needed internally for now.
- **Distribution v2 (Phase 9 future):** Inno Setup — single `.exe` installer, handles file extraction, shortcuts, optional "Start with Windows" checkbox, admin elevation. ~60-line `.iss` script, zero code changes to the app.

---

## 5. Known gotchas

1. **WPF-UI `FluentWindow` has a reported memory leak** on long-running sessions (open GitHub issue on lepoco/wpfui). For a tray-resident work app this could matter over days. Mitigation: monitor in smoke testing; worst case fall back to `Window` with `WindowChrome` + custom title bar.
2. **`strict_route: false` means brief leak window if sing-box crashes hard.** During a clean disconnect, sing-box removes the TUN adapter properly. During a hard crash, the adapter may persist in a broken state until reboot or manual `netsh int show interfaces`. Mitigation: on app startup, scan for orphaned `vytswell_tun` adapters and delete them via `netsh interface set interface vytswell_tun admin=disabled` before starting fresh.
3. **sing-box TUN requires `wintun.dll`** in the same folder as `sing-box.exe`. Ship it or TUN silently fails.
4. **sing-box config schema changes across versions.** Pin the exact version (e.g., 1.11.x) in repo, test once, don't auto-update unless you retest `FixedPolicy.cs`. Especially: `rule_set update_interval` was added in 1.8 — don't pin older.
5. **Admin elevation breaks drag-drop from Explorer** (different integrity levels). Not relevant for our flow (paste only), but note if requirements ever change.
6. **Deleting `Handler/SubscriptionHandler` and related SQLite tables:** if existing users have a DB from full v2rayN, leave the unused tables alone — SQLite doesn't care, and migrating schemas adds risk with zero benefit for a new install.
7. **GeoSite SRS files vary in size.** `geoip-ru.srs` is ~5 MB; `geosite-category-ads-all.srs` ~2 MB; `geosite-private.srs` ~1 MB. Total seed bundle adds ~8 MB to the installer — well within acceptable range.
8. **ReactiveUI.Fody removal:** if you drop it, replace `[Reactive] T Prop { get; set; }` with explicit `this.RaiseAndSetIfChanged(ref _field, value)` in the ~150 lines of ViewModel code. Trivial, and removes an IL-weaving dependency.
9. **GitHub-hosted geosite URL may be blocked at workplace.** `raw.githubusercontent.com` is sometimes filtered. Have a Plan B: a workplace-mirrored URL (e.g., internal HTTPS server hosting the same `.srs` files), configurable via a registry key or build constant. Don't expose this in the UI — keep config hardcoded.
10. **Russian Windows + globe icon:** make sure the chosen Unicode glyph (🌐 U+1F310) renders on Windows 10 Segoe UI Emoji and Windows 11 Segoe Fluent Icons. Safer choice: WPF-UI's `<ui:SymbolIcon Symbol="Globe24" />`.
11. **Hotkey conflicts:** `Ctrl+Alt+P` may collide with Office "Print" or other apps. Pick a less common default (e.g., `Ctrl+Alt+Shift+P`) and let user rebind. If `RegisterHotKey` fails (already taken), show toast "Hotkey unavailable" — don't crash.
12. **Auto-update of geosites needs internet on first launch.** If first launch is offline, sing-box uses bundled seed files — verify they're freshly bundled at release time, not stale 6-month-old copies.
13. **TUN mode + corporate Always-On VPN / Cisco AnyConnect** can deadlock routing tables. Test on a workplace machine that already has a VPN client installed.

---

## 6. Estimated total effort

| Phase | Hours |
|---|---|
| 0 — Branch & baseline | 0.5 |
| 1 — Solution cleanup | 1 |
| 2 — Package swap | 0.5 |
| 3 — Strip ServiceLib | 2–3 |
| 4 — FixedPolicy enforcement (incl. remote rule_set wiring) | 2.5 |
| 5 — WPF UI rebuild + Russian/English resx + globe switch | 4–5 |
| 6 — Tray / single-instance / elevation / hotkey | 1.5 |
| 7 — sing-box distribution + seed `.srs` files | 1 |
| 8 — Smoke testing | 1.5 |
| 9 — Polish | 1–2 |
| **Total** | **~15–18 focused hours** |

Spread over 2–3 working sessions with build breaks, realistic **3–5 calendar days** solo.

---

## 7. Out of scope (future, if ever)

- Auto-update (re-add `AmazTool` or use Velopack).
- Auto-connect on startup.
- Multi-user profiles / enterprise provisioning (MDM push of connection URLs).
- Telemetry / crash reporting.
- Localization.
- Per-app routing (sing-box supports it via `process_name` rules but needs user education).

---

## 8. Decisions made

- **Multiple saved connections** — keep the Connections dialog; users can store more than one profile.
- **Auto-connect on launch** — Yes. Toggle in Settings; default off.
- **Logs delivery** — "Экспортировать логи" button in Settings (zips local logs to user-chosen path). No telemetry, no auto-send.
- **Hotkey** — togglable on/off in Settings, default off; binding configurable; default shortcut TBD by user later.
- **Globe icon** — `<ui:SymbolIcon Symbol="Globe24"/>` in title bar, single-click RU↔EN toggle.
- **sing-box errors** — displayed verbatim in English inside InfoBar (no translation map).
- **Workplace VPN coexistence** — out of scope. The app assumes it is the only network client on the system.
- **Geosite mirror URL** — parked. If `raw.githubusercontent.com` turns out blocked at workplace, revisit then; build constant ready for swap.
- **Orphaned TUN adapter cleanup on startup** — implemented in Phase 6 to mitigate `strict_route: false` hard-crash leftovers.
- **Settings dialog content** — confirmed (auto-start, auto-connect, theme, hotkey toggle+capture, geosite update info, export logs, log folder, version).
- **Bootstrap seed `.srs`** — must be freshly bundled at each release (added to release checklist below).

## 9. Still open (decide later, not blocking start of work)

1. **Default hotkey shortcut** — pick when ready; not blocking since toggle is off by default.
2. **Manual "Update lists now" UX** — silent restart with progress ring, or warn user that connection drops for ~2 s? Default plan: silent with progress ring; revisit if it feels wrong in testing.
3. **Connection list ordering** — alphabetical, last-used, or manual drag? Default plan: insertion order, no drag. Cheap to change later.

## 10. Release checklist (when you cut a build)

- [ ] `Resources/rule_sets/*.srs` re-downloaded from sing-geosite (≤ 1 day old)
- [ ] `Resources/bin/sing-box/sing-box.exe` matches pinned version in `Directory.Build.props`
- [ ] `wintun.dll` present beside `sing-box.exe`
- [ ] `app.manifest` still requests `requireAdministrator`
- [ ] Smoke matrix from Phase 8 run on a clean Windows VM
- [ ] No Russian text missing from `Strings.resx` (run a check that every key in `Strings.en.resx` exists in `Strings.resx`)
