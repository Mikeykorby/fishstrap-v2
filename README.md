# Fishstrap V2

A custom bootstrapper for Roblox, rebuilt from the ground up. Fishstrap V2 is a
fork of Fishstrap (which is based on Bloxstrap by pizzaboxer) with a fully
redesigned dark interface and a set of new quality-of-life features.

Fishstrap V2 is an application for **Windows 10 and above**.

## Features

- **Redesigned dashboard-first settings UI** — dark theme, quick launch,
  quick settings, quick links and live statistics on one screen
- **Roblox deployment management** — real deploy API integration
  (clientsettings.roblox.com + setup.rbxcdn.com), channel switching,
  version history with pinning, reinstall, Studio support and old-version pruning
- **Unhidden FastFlags editor** — editable flag grid with search, presets,
  JSON import/export, and Fishstrap-style allowlist enforcement
- **Global basic settings** — FPS unlock slider, lighting technology,
  graphics backend, post effects, player shadows and telemetry blocking,
  all translated into FastFlags automatically
- **Mods** — custom cursors, sounds and fonts, applied to the installed
  version on every launch, with one-click apply/clean
- **Integrations** — Discord Rich Presence (best-effort log tailing),
  activity tracking, better matchmaking and fishstrap.app game invite links
- **Shortcuts** — desktop / Start menu shortcuts, plus extra shortcuts for
  settings and your last played game
- **Statistics** — launches per day, total playtime, average session and a
  14-day activity chart
- **Profiles & backups** — save the whole settings state under a named
  profile, or create one-click restore-point backups
- **Auto-update checks** against this repository's GitHub releases
- **Test mode** — toggle in the sidebar: changes are held in memory and only
  written to disk when you press Save
- **Cache cleaner** — clears the Roblox HTTP cache, download cache and logs

## Building

Requires the .NET 8 SDK (or newer; a .NET 9 SDK builds the net8.0-windows
target fine):

```
dotnet build FishstrapV2.sln
```

Run the app from `FishstrapV2/bin/Debug/net8.0-windows/FishstrapV2.exe`.

There are **no third-party NuGet dependencies** — everything is built on the
base class libraries, so restore is instant and offline-friendly.

## Where things live

- Settings: `%LOCALAPPDATA%\FishstrapV2\settings.json`
- Statistics: `%LOCALAPPDATA%\FishstrapV2\statistics.json`
- Roblox versions: `%LOCALAPPDATA%\FishstrapV2\Versions`
- Mods: `%LOCALAPPDATA%\FishstrapV2\Mods`
- Logs: `%LOCALAPPDATA%\FishstrapV2\logs`

## Credits

Fishstrap V2 is a fork of [Fishstrap](https://github.com/fishstrap/fishstrap)
(MIT, Copyright (c) 2025 returnrqt), which is based on
[Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) by pizzaboxer (MIT).
Roblox is a trademark of Roblox Corporation; this project is not affiliated
with or endorsed by Roblox Corporation.

Licensed under the [MIT License](LICENSE).
