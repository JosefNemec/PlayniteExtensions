About
---------

Extensions I've made for [Playnite](https://github.com/JosefNemec/Playnite) launcher. Some of these can be downloaded during first time startup when launching Playnite for the first time.

Installation
---------

You can install these via Playnite's built-in add-on browser or opening following URI links (Playnite 9 and newer required):

### Integration plugins

`Amazon / Twitch` playnite://playnite/installaddon/AmazonLibrary_Builtin  
`Battle.net` playnite://playnite/installaddon/BattlenetLibrary_Builtin  
`Bethesda` playnite://playnite/installaddon/BethesdaLibrary_Builtin  
`Epic store` playnite://playnite/installaddon/EpicGamesLibrary_Builtin  
`GOG` playnite://playnite/installaddon/GogLibrary_Builtin  
`Humble` playnite://playnite/installaddon/HumbleLibrary_Builtin  
`itch.io` playnite://playnite/installaddon/ItchioLibrary_Builtin  
`Origin` playnite://playnite/installaddon/OriginLibrary_Builtin  
`Rockstar launcher` playnite://playnite/installaddon/Rockstar_Games_Library  
`Steam` playnite://playnite/installaddon/SteamLibrary_Builtin  
`Ubisoft Connect` playnite://playnite/installaddon/UplayLibrary_Builtin  
`Xbox / MS Store` playnite://playnite/installaddon/XboxLibrary_Builtin  

### Metadata plugins

`IGDB` playnite://playnite/installaddon/IGDBMetadata_Builtin  
`Universal Steam` playnite://playnite/installaddon/Universal_Steam_Metadata

### Other extensions

`Simple library exporter script` playnite://playnite/installaddon/LibraryExporterPS_Builtin)  

Issues
---------

If something doesn't work, open [new issue](https://github.com/JosefNemec/PlayniteExtensions/issues) please. Make sure that you check [wiki](https://github.com/JosefNemec/PlayniteExtensions/wiki) first for troubleshooting tips.

Contributions
---------

Regarding code styling, there are only a few major rules:

- private fields and properties should use camelCase (without underscore)
- all methods (private and public) should use PascalCase
- use spaces instead of tabs with 4 spaces width
- always encapsulate the code body after *if, for, foreach, while* etc. with curly braces:

```csharp
if (true)
{
    DoSomething()
}
```

instead of

```csharp
if (true)
    DoSomething()
```