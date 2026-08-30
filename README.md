<div align="center">

![][banner-light]
![][banner-dark]

**A new launcher, a new experience.**

![][badge-license]
![][badge-builds]
![][badge-downloads]
[![][badge-latest]][repo-latest]
![][badge-stars]

</div>

> [!CAUTION]
> The only official place to download Fishstrap V2 is this GitHub repository.
> Any other websites offering downloads or claiming to be us are not controlled
> by us — do not download from them.

Fishstrap V2 (pronounced fish-strap two) is a custom bootstrapper for Roblox,
rebuilt from the ground up as a fork of [Fishstrap][fishstrap] — which is based
on [Bloxstrap][bloxstrap] — with a fully redesigned dark interface and a set of
new quality-of-life features.

If you found any bugs, please [open an issue here][repo-new-issue].

> [!NOTE]
> Fishstrap V2 is an application for **Windows 10 and above.**

## Features

- Redesigned dashboard-first settings UI — quick launch, quick settings, quick
  links and live statistics on one dark screen
- Detailed server information using [RoValra][rovalra]'s API
- Support for Roblox Studio
- Unhidden FastFlags editor with search, presets and JSON import/export
  - You cannot apply FastFlags not present in the allowlist. This does not
    affect Roblox Studio. [Learn more][devforum-fflags]
- Global basic settings editor — frame rate cap, lighting technology, graphics
  backend, post effects, player shadows and telemetry blocking
- Bootstrapper styles — Fishstrap, Classic Fluent, Terminal and TwentyFive
  dialogs, the Bloxnified theme, or import any Bloxstrap-style custom theme
- Roblox deployment management — channel switching, version history with
  pinning, reinstall and old-version pruning
- Mods — custom cursors, sounds and fonts, applied on every launch
- Integrations — Discord Rich Presence, activity tracking and game invites
- Cache cleaner, desktop/Start menu shortcuts, profiles and one-click backups
- Statistics — launches per day, total playtime and a 14-day activity chart

<div align="center">

![][showcase]

</div>

## Special thanks

- [returnrqt](https://github.com/returnrqt) and the Fishstrap team for the
  original Fishstrap
- [pizzaboxer](https://github.com/pizzaboxer) for Bloxstrap
- [Svznify](https://github.com/Svznify) for the Bloxnified bootstrapper themes
- [Valra](https://github.com/NotValra) for providing their API
- Other independent contributors

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

## License

Fishstrap V2 is a fork of [Fishstrap][fishstrap] (MIT, © 2025 returnrqt), which
is based on [Bloxstrap][bloxstrap] by pizzaboxer (MIT). Roblox is a trademark
of Roblox Corporation; this project is not affiliated with or endorsed by
Roblox Corporation.

Licensed under the [MIT License](LICENSE).

[banner-light]: https://github.com/Mikeykorby/fishstrap-v2/raw/main/Images/Bloxstrap-full-light.png#gh-light-mode-only
[banner-dark]: https://github.com/Mikeykorby/fishstrap-v2/raw/main/Images/Bloxstrap-full-dark.png#gh-dark-mode-only
[showcase]: https://github.com/Mikeykorby/fishstrap-v2/raw/main/Images/Showcase.webp
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
