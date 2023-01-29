# RocksmithRichPresence

![Demo](../assets/demo.png?raw=true)

A small app to add Discord Rich Presence for Rocksmith 2014.

Displays the current song being played, and what state the user is currently in (e.g. playing a song, in the menus etc).

> **Note**
> Corrupt CDLC is common so they may not be able to be fully parsed.

## Cloning and Building

Run the clone command.
```sh-session
git clone https://github.com/Glowstudent777/RocksmithRichPresence.git
```
```sh-session
cd RocksmithRichPresence
```

Clone and update the submodules.
```sh-session
git submodule init
```

```sh-session
git submodule update
```

Restore dependancies.
```sh-session
dotnet restore
```

Building.
```sh-session
dotnet build
```

## Credits

- <a href="https://github.com/kokolihapihvi/">kokolihapihvi</a> - Rocksnifferlib and PsarcLib.
- <a href="https://github.com/brattonross/">brattonross</a> - Base code.
