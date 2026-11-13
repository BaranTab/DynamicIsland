# ⚙️ Settings Guide

All of Dynamic Island's settings live in a single JSON file. This guide covers where to find it, what every field does, and how to connect the features that need an account (Mail and Calendar), step by step.

## Contents

1. [Where is the settings file?](#1-where-is-the-settings-file)
2. [Editing rules](#2-editing-rules)
3. [Full example](#3-full-example)
4. [Language](#4-language--language)
5. [Weather](#5-weather--weather)
6. [Mail](#6-mail--mail)
7. [Calendar](#7-calendar--calendar)
8. [Claude Code](#8-claude-code--claudecode)
9. [VS Code](#9-vs-code--vscode)
10. [Notes](#10-notes)
11. [Troubleshooting](#11-troubleshooting)
12. [Resetting settings](#12-resetting-settings)

---

## 1. Where is the settings file?

On first launch the app creates this file with default values:

```
%AppData%\DynamicIsland\settings.json
```

That is usually `C:\Users\<your-name>\AppData\Roaming\DynamicIsland\settings.json`.

**Three ways to open it:**

| Way | How |
|---|---|
| From the app | Hover the island → **⋯** (top right) → **Open settings file** (opens in Notepad) |
| Run dialog | `Win + R` → type `%AppData%\DynamicIsland` → Enter → `settings.json` |
| From a page | When Mail or Calendar isn't set up, that page shows an **Open settings** button |

> 💡 The settings file is stored in your user profile, **not** in the repository folder, so passwords can never be committed to GitHub by accident. The repo's [`settings.example.json`](../settings.example.json) is only an example; the app never reads it.

---

## 2. Editing rules

- **Restart the app after editing.** Settings are only read at startup (⋯ → Exit, then launch it again). The language is the one exception: it can be switched live from the menu.
- **Text goes in double quotes:** `"Username": "you@gmail.com"`
- **Numbers have no quotes and use a DOT as the decimal separator:** `"Latitude": 41.20` ✅ ・ `"Latitude": 41,20` ❌
- **Mind the commas between lines.** A trailing comma after the last item of a section is fine, but a missing comma between two lines makes the file unreadable.
- **Windows paths need doubled backslashes**, or use forward slashes instead:
  `"C:\\Users\\me\\projects"` ✅ ・ `"C:/Users/me/projects"` ✅ ・ `"C:\Users\me\projects"` ❌
- **`//` comments are allowed.** The app ignores them.
- **If the file is broken**, the app doesn't crash; it starts with default settings and leaves your file untouched. If something doesn't work, paste the file into a [JSON validator](https://jsonlint.com) first, with your password removed.

---

## 3. Full example

```jsonc
{
  "Language": "en",
  "Weather": {
    "Latitude": 51.51,
    "Longitude": -0.13,
    "LocationName": "London"
  },
  "Mail": {
    "ImapHost": "imap.gmail.com",
    "ImapPort": 993,
    "Username": "you@gmail.com",
    "AppPassword": "abcd efgh ijkl mnop",
    "WebUrl": "https://mail.google.com"
  },
  "Calendar": {
    "IcsUrl": "https://calendar.google.com/calendar/ical/.../private-.../basic.ics",
    "WebUrl": "https://calendar.google.com"
  },
  "ClaudeCode": {
    "Projects": [
      "C:/Users/me/projects/portfolio"
    ]
  },
  "VsCode": {
    "CodePath": null,
    "ExtraFolders": []
  }
}
```

You can leave any section you don't use as it is. Features without settings show setup instructions on their page.

---

## 4. Language — `Language`

| Value | Language |
|---|---|
| `"en"` | English (default) |
| `"tr"` | Türkçe |
| `"de"` | Deutsch |
| `"fr"` | Français |
| `"es"` | Español |
| `"it"` | Italiano |
| `"pt"` | Português |

The easiest way to change it is from the app: **⋯ → Language**. The UI switches instantly and your choice is saved to this file. Dates, weekday names and month names follow the selected language too.

> Want to add a language? Copy `src/DynamicIsland/Localization/Languages/en.json`, translate the values, and add one line to the list in `Localization/Loc.cs`. Missing keys automatically fall back to English.

---

## 5. Weather — `Weather`

Weather comes from [Open-Meteo](https://open-meteo.com). It's free and needs no account or API key. It updates every 15 minutes.

| Field | Type | Default | Description |
|---|---|---|---|
| `Latitude` | number | `41.20` | Latitude (north is positive) |
| `Longitude` | number | `32.62` | Longitude (east is positive, west is negative) |
| `LocationName` | text | `"Karabük"` | Only the name shown on screen; doesn't affect the location |

### Finding your coordinates

1. Open [Google Maps](https://maps.google.com) and find your city.
2. **Right-click** the spot you want.
3. Two numbers such as `51.51, -0.13` appear at the top of the menu. Click them to copy.
4. Put the first number in `Latitude` and the second in `Longitude`.

```json
"Weather": { "Latitude": 51.51, "Longitude": -0.13, "LocationName": "London" }
```

---

## 6. Mail — `Mail`

The Mail page shows your **unread count** and your **3 most recent emails**, and refreshes every 2 minutes.

- It connects over **IMAP** in **read-only** mode. The app never marks emails as read, deletes them or sends anything.
- Clicking an email opens `WebUrl` in your browser.

| Field | Type | Default | Description |
|---|---|---|---|
| `ImapHost` | text | `"imap.gmail.com"` | Your provider's IMAP server (**not your email address!**) |
| `ImapPort` | number | `993` | SSL port; almost always 993 |
| `Username` | text | `""` | Your full email address |
| `AppPassword` | text | `""` | An **app password** (not your normal password) |
| `WebUrl` | text | `"https://mail.google.com"` | What the "Open inbox" button opens |

> ⚠️ **Common mistake:** putting your email address in `ImapHost`. `ImapHost` is always a server name such as `imap.gmail.com`; your email address goes in `Username`.

### Gmail, step by step

Gmail doesn't let third-party apps sign in with your normal password. Instead you create a one-off **app password** just for this app.

**Step 1: Turn on 2-Step Verification**
1. Go to [myaccount.google.com/security](https://myaccount.google.com/security).
2. If **2-Step Verification** is off, turn it on. App passwords only work while it's enabled.

**Step 2: Create an app password**
1. Go to [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords).
2. Enter `Dynamic Island` as the app name → **Create**.
3. A 16-letter password such as `abcd efgh ijkl mnop` appears. **It's only shown once**, so copy it right away.

**Step 3: Make sure IMAP is enabled in Gmail**
1. Gmail → ⚙️ (top right) → **See all settings**.
2. **Forwarding and POP/IMAP** tab → **Enable IMAP** → Save changes.

**Step 4: Add it to the settings file**
```json
"Mail": {
  "ImapHost": "imap.gmail.com",
  "ImapPort": 993,
  "Username": "you@gmail.com",
  "AppPassword": "abcd efgh ijkl mnop",
  "WebUrl": "https://mail.google.com"
}
```
The spaces in the password are optional; it works either way.

**Step 5:** Restart the app and open the Mail tab.

### Other providers

| Provider | `ImapHost` | Password | `WebUrl` |
|---|---|---|---|
| Gmail | `imap.gmail.com` | App password | `https://mail.google.com` |
| Yandex | `imap.yandex.com` | App password (Yandex ID → Security → App passwords) | `https://mail.yandex.com` |
| iCloud | `imap.mail.me.com` | App-specific password (account.apple.com) | `https://www.icloud.com/mail` |
| Yahoo | `imap.mail.yahoo.com` | App password (Account security) | `https://mail.yahoo.com` |
| Outlook / Hotmail | — | ❌ Not supported yet | — |

> Microsoft has disabled password sign-in for IMAP on personal Outlook/Hotmail accounts and only allows OAuth. OAuth support is on the [roadmap](../README.md#roadmap).

### Status messages on the Mail page

The small text next to "Recent email" tells you what's going on:

| Message | Meaning / fix |
|---|---|
| `Account not connected` | `Username` or `AppPassword` is empty. Fill them in and restart |
| `Updating…` | Connecting, wait a few seconds |
| `Updated 14:32` | All good ✅ |
| `Sign-in failed…` | Wrong username or app password. Make sure it isn't your normal password; create a new app password if needed |
| `Couldn't connect: …` | Wrong server name or port, no internet, or IMAP is disabled. Check `ImapHost` and Gmail's IMAP setting |

Use the **⟳** button to refresh without waiting 2 minutes.

### 🔐 Security

- The app password is stored as **plain text** in `settings.json`. Anyone with access to your PC could read it.
- That's why you should never use your normal Google password here. An app password only grants mail access and can be revoked from your Google Account with one click.
- If you ever shared your password in a screenshot, an issue or a commit, delete it on the [app passwords page](https://myaccount.google.com/apppasswords) and create a new one.
- Your mail details are never sent anywhere other than your own mail server.

---

## 7. Calendar — `Calendar`

The Calendar page shows a month view and your next 3 events, with a green dot under days that have events. Events are read from your provider's **iCal (.ics) address** and refreshed every 15 minutes.

| Field | Type | Default | Description |
|---|---|---|---|
| `IcsUrl` | text | `""` | Your calendar's iCal address (`https://` or `webcal://`) |
| `WebUrl` | text | `"https://calendar.google.com"` | What the ↗ button opens |

### Google Calendar, step by step

1. Open [calendar.google.com](https://calendar.google.com) on a computer. This setting isn't available in the mobile app.
2. Under **My calendars** on the left, hover your calendar → **⋮** → **Settings and sharing**.
3. Scroll down to **Integrate calendar**.
4. Copy the **Secret address in iCal format**. It looks like `https://calendar.google.com/calendar/ical/.../private-.../basic.ics`.
   - Use the **secret** address, not the public one. The public address only works if your calendar is public.
5. Paste it into `IcsUrl` and restart the app.

```json
"Calendar": {
  "IcsUrl": "https://calendar.google.com/calendar/ical/you%40gmail.com/private-abc123/basic.ics",
  "WebUrl": "https://calendar.google.com"
}
```

> ⚠️ Anyone with the secret address can see your calendar, so don't share it. If you leak it by accident, use the **Reset** button on the same settings page to get a new one.

### Outlook Calendar

1. [outlook.live.com/calendar](https://outlook.live.com/calendar) → ⚙️ **Settings** → **Calendar** → **Shared calendars**.
2. Under **Publish a calendar**, pick your calendar and "Can view all details" → **Publish**.
3. Copy the **ICS** link and paste it into `IcsUrl`.

### Known limitations

- **Recurring events** (a weekly class, for example) currently only show their **first occurrence**.
- Only events in the **next 60 days** are shown.
- Events with a time zone are treated as your computer's local time.

---

## 8. Claude Code — `ClaudeCode`

The Claude Code page lists the **3 projects you most recently worked on** with [Claude Code](https://claude.com/claude-code). Click a project to open a new terminal in that folder and run `claude`.

**It works without any settings.** Projects are detected automatically: Claude Code saves each session under `%UserProfile%\.claude\projects\`, and the app reads each project's folder from those records.

| Field | Type | Default | Description |
|---|---|---|---|
| `Projects` | list of text | `[]` | Folders to show **in addition** to the detected ones |

```json
"ClaudeCode": {
  "Projects": [
    "C:/Users/me/projects/portfolio",
    "D:\\work\\agency-site"
  ]
}
```

**Requirements**
- The `claude` command must work in a terminal. Check it by opening a terminal and running `claude --version`.
- If [Windows Terminal](https://aka.ms/terminal) is installed, sessions open there; otherwise in the classic Command Prompt.

**Buttons**
- **Open last project:** starts a session in the first project of the list. If the list is empty, it asks you to pick a folder.
- **Choose folder…:** starts a new session in any folder you pick.

---

## 9. VS Code — `VsCode`

The VS Code page lists the **3 folders or files you most recently opened** in VS Code. Click one to open it.

**It works without any settings.** The list comes from VS Code's own "recently opened" record (`%AppData%\Code\User\globalStorage\state.vscdb`). Remote connections (SSH, WSL) aren't shown.

| Field | Type | Default | Description |
|---|---|---|---|
| `CodePath` | text / `null` | `null` | Path to `Code.exe`. Leave `null` to detect it automatically |
| `ExtraFolders` | list of text | `[]` | Folders added when the list has fewer than 3 items |

You only need `CodePath` if VS Code is installed somewhere unusual (for example, the portable version):

```json
"VsCode": {
  "CodePath": "D:/Tools/VSCode/Code.exe",
  "ExtraFolders": [ "C:/Users/me/projects" ]
}
```

Locations checked automatically:
- `%LocalAppData%\Programs\Microsoft VS Code\Code.exe` (user install, the most common)
- `%ProgramFiles%\Microsoft VS Code\Code.exe` (system install)
- If neither exists, the `code` command on your PATH

> VS Code Insiders' recent list isn't read. If you use Insiders, add your folders to `ExtraFolders`.

---

## 10. Notes

The Notes page has no settings. What you type is saved automatically about half a second after you stop, to:

```
%AppData%\DynamicIsland\notes.json
```

- **＋** creates a new note, **🗑** deletes the open one.
- The list on the right shows your 4 most recent notes.
- Empty notes aren't saved.
- The island stays open while you type and closes when you click another window.

---

## 11. Troubleshooting

<details>
<summary><b>I changed a setting but nothing happened</b></summary>

Settings are only read at startup. Close the app with ⋯ → **Exit** and start it again. If it still doesn't work, the file may contain a JSON error: check it on [jsonlint.com](https://jsonlint.com) (remove your password first).
</details>

<details>
<summary><b>The Mail page says "Account not connected"</b></summary>

`Username` or `AppPassword` is empty. Make sure your email address didn't end up in the `ImapHost` field by mistake.
</details>

<details>
<summary><b>"Sign-in failed" error</b></summary>

- Make sure you entered an **app password**, not your normal Gmail password.
- If you deleted the app password, create a new one.
- Google sometimes blocks new connections. Sign in to Gmail in a browser once and check for a security alert.
</details>

<details>
<summary><b>No events in the calendar</b></summary>

- Make sure you used the **secret** iCal address, not the public one.
- If pasting the address into a browser downloads an `.ics` file, the address is correct.
- The event may be more than 60 days away, or it may be a recurring event (see [limitations](#known-limitations)).
</details>

<details>
<summary><b>The Claude Code page is empty</b></summary>

The list stays empty until you've used Claude Code at least once. Run `claude` in a project, exit, and the list fills up. You can also add folders manually under `ClaudeCode.Projects`.
</details>

<details>
<summary><b>Clicking a project opens a terminal that says "claude is not recognized"</b></summary>

Claude Code isn't installed or isn't on your PATH. Follow the [setup instructions](https://docs.claude.com/en/docs/claude-code/setup), then check with `claude --version` in a new terminal.
</details>

<details>
<summary><b>VS Code doesn't open</b></summary>

If VS Code is installed somewhere unusual, put the full path to `Code.exe` in `VsCode.CodePath`.
</details>

<details>
<summary><b>The weather is stuck on "Loading…"</b></summary>

Check your internet connection. Make sure the coordinates use a **dot**, not a comma (`41.20` ✅, `41,20` ❌).
</details>

---

## 12. Resetting settings

Delete `settings.json` and restart the app; it's recreated with default values. Your notes are in a separate file (`notes.json`), so they're kept.

To remove everything the app has stored, delete the `%AppData%\DynamicIsland` folder.
