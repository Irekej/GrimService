# GrimService

Server and web administration project for Gran Turismo 5 and Gran Turismo 6.
Target runtime: .NET 8; database: PostgreSQL.

Before running, copy `appsettings.example.json` to `appsettings.json` and supply
your own database credentials, signing secret and HTTPS certificate. Never commit
your real configuration, private keys, player data or installation archives.
Game resources and console/mod binaries must be supplied separately.

```powershell
dotnet restore
dotnet build
```
Project inspirated in 2022 by GTGRIMSERVER (now Grimoire)