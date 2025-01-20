# Orleans Playground

You have to manually run the `Main` and `Persistance` Postgres scripts into your localhost instance, see https://learn.microsoft.com/en-us/dotnet/orleans/host/configuration-guide/adonet-configuration.

The default connection string is hardcoded as

```csharp
options.ConnectionString = "Host=localhost;Port=5432;Database=OrleansPlayground;Username=postgres;Password=postgres";
```
