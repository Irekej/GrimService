# GrimService

Server and web administration software for Gran Turismo 5 and Gran Turismo 6.

**Target runtime:** .NET 8  
**Database:** PostgreSQL

## Configuration

Inside the `GrimService` directory, copy `appsettings.example.json`
to `appsettings.json` and configure your database connection,
JWT signing secret, and HTTPS certificate.

Never commit production configuration, private keys, player data,
or installation archives.

## Building

From the repository root:

```sh
dotnet restore
dotnet build
```

## Background

Development was inspired in 2022 by GTGrimServer, now known as Grimoire.

Some static resource files remain similar to that project.

## License

See [LICENSE.txt](LICENSE.txt) for permitted uses and contribution terms.
This project is source-available, not licensed under MIT.
