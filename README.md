> [!CAUTION]
> The only official place to download Fishstrap V2 is this GitHub repository.
> Any other websites offering downloads or claiming to be us are not controlled
> by us, do not download from them.

<div align="center">

![][shot-dashboard]

**A new launcher, a new experience.**

![][badge-license]
![][badge-builds]
![][badge-downloads]
[![][badge-latest]][repo-latest]
![][badge-stars]

</div>

Fishstrap V2 is a custom bootstrapper for Roblox, rebuilt from scratch around
a sleek, dark, dashboard-first interface. Everything important lives on one
screen — quick launch for Player and Studio, quick settings, quick links and
live statistics — with FastFlags, mods, bootstrapper themes, version management
and more a click away in the sidebar.

It is a fork of [Fishstrap][fishstrap], which is based on [Bloxstrap][bloxstrap].

If you found any bugs, please [open an issue here][repo-new-issue].

> [!NOTE]
> Fishstrap V2 is an application for **Windows 10 and above.** The project is
> under active development — some screens and features are still being polished.

## Features

- A sleek dashboard-first interface — quick launch, quick settings, quick links
  and live statistics on one dark screen
- Detailed server information using [RoValra][rovalra]'s API
- Support for Roblox Studio
- Unhidden FastFlags editor with search, presets and JSON import/export
  - You cannot apply FastFlags not present in the allowlist. This does not
    affect Roblox Studio. [Learn more][devforum-fflags]
- Global basic settings editor
  - Ability to increase frame rate cap, switch lighting technology and graphics
    backend, toggle post effects and player shadows, block telemetry and more
- Fishstrap's own game invites
- Multiple bootstrapper styles — the Fishstrap dialog, Classic Fluent, Terminal
  and TwentyFive, the Bloxnified theme, or import any Bloxstrap-style custom
  theme
- Channel switcher, version pinning, reinstall and old-version pruning
- Cache cleaner, desktop/Start menu shortcuts, flag profiles and one-click
  backups
- Mods — custom cursors, sounds and fonts, applied on every launch
- Discord Rich Presence and activity tracking
- Launch statistics with a 14-day activity chart

<div align="center">

![][shot-fastflags]

*The unhidden FastFlags editor — search, presets and one-click apply.*

</div>

## Special thanks

- [returnrqt](https://github.com/returnrqt) and the Fishstrap team for the
  original Fishstrap
- [pizzaboxer](https://github.com/pizzaboxer) for Bloxstrap
- [Svznify](https://github.com/Svznify) for the Bloxnified bootstrapper themes
- [Valra](https://github.com/NotValra) for providing their API
- Other independent contributors

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or
newer:

```
git clone https://github.com/Mikeykorby/fishstrap-v2.git
cd fishstrap-v2
dotnet build FishstrapV2.sln
```

The app builds to `FishstrapV2/bin/Debug/net8.0-windows/FishstrapV2.exe`.
There are no third-party NuGet dependencies — everything is built on the base
class libraries, so restore is instant and offline-friendly.

## Where things live

- Settings: `%LOCALAPPDATA%\FishstrapV2\settings.json`
- Statistics: `%LOCALAPPDATA%\FishstrapV2\statistics.json`
- Roblox versions: `%LOCALAPPDATA%\FishstrapV2\Versions`
- Mods: `%LOCALAPPDATA%\FishstrapV2\Mods`
- Logs: `%LOCALAPPDATA%\FishstrapV2\logs`

## License

Fishstrap V2 is a fork of [Fishstrap][fishstrap] (MIT, © 2025 returnrqt), which
is based on [Bloxstrap][bloxstrap] by pizzaboxer (MIT). Roblox is a trademark
of Roblox Corporation; this project is not affiliated with or endorsed by
Roblox Corporation.

Licensed under the [MIT License](LICENSE).

[shot-dashboard]: https://github.com/Mikeykorby/fishstrap-v2/raw/main/Images/Screenshots/Dashboard.png
[shot-fastflags]: https://github.com/Mikeykorby/fishstrap-v2/raw/main/Images/Screenshots/FastFlags.png
[badge-license]: https://img.shields.io/github/license/Mikeykorby/fishstrap-v2?style=flat-square
[badge-builds]: https://img.shields.io/github/actions/workflow/status/Mikeykorby/fishstrap-v2/ci-release.yml?branch=main&style=flat-square&label=builds
[badge-downloads]: https://img.shields.io/github/downloads/Mikeykorby/fishstrap-v2/latest/total?style=flat-square&color=981bfe
[badge-latest]: https://img.shields.io/github/v/release/Mikeykorby/fishstrap-v2?style=flat-square&color=7a39fb
[badge-stars]: https://img.shields.io/github/stars/Mikeykorby/fishstrap-v2?style=flat-square&color=dd9900
[repo-latest]: https://github.com/Mikeykorby/fishstrap-v2/releases/latest
[repo-new-issue]: https://github.com/Mikeykorby/fishstrap-v2/issues/new
[fishstrap]: https://github.com/fishstrap/fishstrap
[bloxstrap]: https://bloxstraplabs.com
[rovalra]: https://www.rovalra.com
[devforum-fflags]: https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569
