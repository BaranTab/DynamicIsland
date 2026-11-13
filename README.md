<div align="center">

# 🏝️ Dynamic Island for Windows

**A glassy control centre that lives at the top of your screen, inspired by the iPhone's Dynamic Island.**
Music, a focus timer, weather, email, calendar, notes and your developer tools, all in one place.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![WPF](https://img.shields.io/badge/UI-WPF-0C54C2)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows)
![Languages](https://img.shields.io/badge/languages-7-orange)
![License](https://img.shields.io/badge/license-MIT-green)

<!-- Screenshot / GIF: add it to docs/images/ and uncomment the line below -->
<!-- ![Demo](docs/images/demo.gif) -->

</div>

---

## ✨ Features

When collapsed, it sits at the top centre of your screen as a small notch. Hover over it and it springs open into a glass panel.

| | Page | What it does |
|---|---|---|
| 🏠 | **Home** | Summary cards for weather, music, focus and mail |
| 🎵 | **Music** | Now playing, album art, progress bar, previous / play / next |
| ⏱ | **Focus** | Pomodoro timer; drag the ruler to pick 1–60 minutes |
| 🌤 | **Weather** | Current conditions, high/low and a 5-hour forecast |
| ✉️ | **Mail** | Unread count and latest emails (IMAP, read-only) |
| 🤖 | **Claude Code** | Recent projects; start a `claude` session in one click |
| 💻 | **VS Code** | Recently opened folders; open them in VS Code in one click |
| 📝 | **Notes** | A quick notepad that saves automatically |
| 📅 | **Calendar** | Month view and upcoming events (Google / Outlook iCal) |

**Also**
- 🎨 **Glass that takes on colour:** while music plays, it picks up the dominant colour of the album art and glows with it from below. Every page has its own accent colour.
- 💧 **Flows out of the screen edge:** the top corners curve inwards into the edge of the screen, with an elastic animation when it opens and closes.
- 🎶 **A live compact island:** shows tiny album art and an animated equalizer while music plays, or a countdown while the focus timer runs.
- 🌍 **7 languages:** English, Türkçe, Deutsch, Français, Español, Italiano, Português. Switch instantly from the ⋯ menu.
- 🎮 **Stays out of the way:** hides itself during full-screen games and videos, doesn't show up in Alt+Tab, and never steals focus from other windows.

The music section works with any app that supports Windows' media controls: Spotify, YouTube in the browser, Apple Music, VLC…

---

## 📦 Installation

### Option 1: Download a release (recommended)

1. Download the latest `DynamicIsland-win-x64.zip` from the [Releases](../../releases) page.
2. Extract it anywhere and run `DynamicIsland.exe`.

You don't need to install .NET. If Windows SmartScreen shows a warning, click **More info → Run anyway**. This is normal for apps that aren't code-signed.

### Option 2: Build from source

**Requirements:** Windows 10 (2004+) or Windows 11, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```powershell
git clone https://github.com/<your-username>/DynamicIsland.git
cd DynamicIsland/src/DynamicIsland
dotnet run
```

The first build downloads NuGet packages, so it may take a moment.

To build your own `.exe`:
```powershell
dotnet publish src/DynamicIsland -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

---

## 🖱️ Usage

| To… | Do this |
|---|---|
| Open the island | Hover over the notch at the top centre of the screen |
| Close it | Move the mouse away |
| Switch pages | Click the tabs at the bottom **or** drag left/right **or** use the mouse wheel **or** swipe with two fingers on a touchpad |
| Jump to a section from Home | Click its card |
| Language, settings, exit | The **⋯** menu (top right) |

---

## ⚙️ Settings

The clock, music, focus timer, notes, Claude Code and VS Code **work without any setup**. The weather shows Karabük, Türkiye by default. Mail and Calendar need your account details.

Settings live in `%AppData%\DynamicIsland\settings.json`. Open it from the app with **⋯ → Open settings file**, and restart the app after editing.

| Section | Used for | Required? |
|---|---|---|
| `Language` | UI language (`en`, `tr`, `de`, `fr`, `es`, `it`, `pt`) | Optional (default: English; also in the ⋯ menu) |
| `Weather` | Your city's coordinates | Optional (default: Karabük) |
| `Mail` | IMAP server, email address, **app password** | For the Mail page |
| `Calendar` | Your calendar's **iCal address** | For calendar events |
| `ClaudeCode` | Extra project folders | Optional (detected automatically) |
| `VsCode` | Path to `Code.exe`, extra folders | Optional (detected automatically) |

### Quick start: Gmail

1. Turn on **2-Step Verification** at [Google Account → Security](https://myaccount.google.com/security).
2. Create a password named `Dynamic Island` on the [App passwords](https://myaccount.google.com/apppasswords) page.
3. In Gmail → Settings → **Forwarding and POP/IMAP**, enable IMAP.
4. Add it to the settings file:

```json
"Mail": {
  "ImapHost": "imap.gmail.com",
  "ImapPort": 993,
  "Username": "you@gmail.com",
  "AppPassword": "abcd efgh ijkl mnop",
  "WebUrl": "https://mail.google.com"
}
```

### Quick start: Google Calendar

1. [calendar.google.com](https://calendar.google.com) → **⋮** next to your calendar → **Settings and sharing**.
2. **Integrate calendar** → copy the **Secret address in iCal format**.
3. Paste it into `Calendar` → `IcsUrl`.

📖 **For every field explained, other mail providers (Yandex, iCloud, Yahoo), Outlook Calendar, finding coordinates and troubleshooting, see the [Settings Guide](docs/SETTINGS.md).**

An example settings file: [`settings.example.json`](settings.example.json)

---

## 🔐 Privacy

- The app collects no data and has no server of its own.
- The only outgoing connections are:
  - your coordinates, sent to [Open-Meteo](https://open-meteo.com) for the weather
  - an IMAP connection to your own mail server
  - the iCal address you provide
- Settings, passwords and notes stay on your computer in `%AppData%\DynamicIsland` and are never part of the repository.
- The app password is currently stored as plain text. That's why it uses a revocable **app password** instead of your real password. Encrypted storage is on the roadmap.

---

## 🧱 Architecture

```
src/DynamicIsland/
├── Modules/                 # Each feature is an independent module (IIslandModule)
│   ├── ClockModule.cs
│   ├── MediaModule.cs       # Windows.Media.Control (GSMTC)
│   ├── WeatherModule.cs     # Open-Meteo
│   ├── FocusModule.cs       # Pomodoro timer
│   ├── MailModule.cs        # IMAP (MailKit)
│   ├── CalendarModule.cs    # iCal reader
│   ├── ClaudeCodeModule.cs  # Recent projects from ~/.claude/projects sessions
│   └── VsCodeModule.cs      # Recent folders from VS Code's state.vscdb (SQLite)
├── ViewModels/
│   └── MainViewModel.cs     # Connects the modules to the UI
├── Views/
│   ├── IslandWindow.xaml    # Island shell: glass shape, compact view, header, tabs
│   └── Pages/               # Each page is its own UserControl
├── Controls/
│   ├── IslandShape.cs       # The glass shape (animated geometry + glow)
│   └── TickRuler.cs         # Ruler for the focus duration
├── Localization/
│   ├── Loc.cs               # Translation service (live language switching)
│   ├── TExtension.cs        # {loc:T Key} XAML markup extension
│   └── Languages/*.json     # en, tr, de, fr, es, it, pt
├── Services/                # Settings, app launcher, notes, colour extraction, Win32
└── Themes/Styles.xaml       # Shared styles
```

**Technical highlights**
- **Click-through transparent window:** only the drawn glass shape captures the mouse; everything else passes through to the apps below. `WS_EX_NOACTIVATE` keeps it from stealing focus, and the flag is lifted temporarily while you type a note.
- **Animated geometry:** the island is redrawn every frame with a `StreamGeometry`. Width, height, corner radius and the "liquid" curve radius are animated independently.
- **Album colour:** the cover is shrunk to 32 px, saturated and bright pixels are weighted more heavily to find the dominant colour, and it's then clamped in HSV to a range that suits the UI.
- **Media:** Windows' Global System Media Transport Controls API provides info from every app. Progress is the app's last reported position plus the time elapsed since.
- **Live localization:** XAML binds to an indexer on a singleton (`{loc:T Key}`). Raising `PropertyChanged("Item[]")` updates every text instantly when the language changes.

### Adding a page
1. Write a module implementing `IIslandModule` under `Modules/` (if the page needs data).
2. Add the module and any UI properties to `MainViewModel`.
3. Create a 636×190 `UserControl` under `Views/Pages/`.
4. Add it to the page strip, the tab bar and the header dots in `IslandWindow.xaml`, and increase `PageCount` in `IslandWindow.xaml.cs`.
5. Add its texts to every file in `Localization/Languages/`.

### Adding a language
1. Copy `Localization/Languages/en.json` to `<code>.json` and translate the values.
2. Add one line to `Loc.Languages` in `Localization/Loc.cs`.

Missing keys fall back to English, so partial translations are fine.

---

## Roadmap

- [ ] Settings window (currently a JSON file)
- [ ] System tray icon
- [ ] Start with Windows
- [ ] Encrypted app password storage (Windows DPAPI)
- [ ] OAuth sign-in for Outlook/Hotmail
- [ ] Recurring calendar events (RRULE)
- [ ] Island animation when volume or brightness changes
- [ ] Battery, notifications and clipboard history pages
- [ ] Multi-monitor support
- [ ] Real background blur (Windows 11 acrylic)

Ideas and bug reports are welcome: open an [issue](../../issues).

---

## 📄 License

[MIT](LICENSE) © 2026 Baran Tabak
