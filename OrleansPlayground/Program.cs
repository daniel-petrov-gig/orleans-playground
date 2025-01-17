using Orleans.Storage;
using OrleansPlayground;

var builder = WebApplication.CreateBuilder(args);

// uncomment below to swap the default Newtonsoft storage serializer with the Orleans binary
// but this means you must now decorate all state objects with [GenerateSerializer] and [Alias]
// builder.Services.AddSingleton<IGrainStorageSerializer, OrleansGrainStorageSerializer>();

builder.Host.UseOrleans(static silo =>
{
    silo.UseLocalhostClustering();
    silo.AddLogStorageBasedLogConsistencyProvider("LogStorage");

    // what can trip you here from util-endeavour is that JSON typename handling is TURNED OFF
    // meaning you cannot store polymorphic types (interfaces and abstract classes) in the state
    // log storage provider naturally require polymorphism to store different "events" (how can Orleans know otherwise which event is which runtime type?)
    // but this has nothing to do with the storage provider, the same thing applies to state storage as well (e.g. you store some abstract class in one of the props)
    // Key takeaways:
    // -> request/response and storage serializers are orthohonal
    // -> storage providers (e.g. event sourcing log consistency providers) and storage serializers (heck, the whole storage itself) are orthogonal too!
    silo.AddAdoNetGrainStorage("accounts", options =>
    {
        options.Invariant = "Npgsql";
        options.ConnectionString = "Host=localhost;Port=5432;Database=OrleansPlayground;Username=postgres;Password=postgres";
    });
});

var app = builder.Build();

app.MapGet("/account/{accountNumber:required}/deposit/{amount:decimal}", static async (IGrainFactory grains, string accountNumber, decimal amount) =>
{
    var account = grains.GetGrain<IBankAccountGrain>(accountNumber);

    await account.Deposit(amount);
});

app.MapGet("/account/{accountNumber:required}/withdraw/{amount:decimal}", static async (IGrainFactory grains, string accountNumber, decimal amount) =>
{
    var account = grains.GetGrain<IBankAccountGrain>(accountNumber);

    await account.Withdraw(amount);
});

app.MapGet("/account/{accountNumber:required}/balance", static async (IGrainFactory grains, string accountNumber, DateTimeOffset? at) =>
{
    var account = grains.GetGrain<IBankAccountGrain>(accountNumber);

    var response = new
    {
        AccountNumber = account.GetPrimaryKeyString(),
        Balance = at is null ? await account.GetBalance() : await account.GetBalance(at.Value),
    };

    return response;
});


app.MapGet("/account/{accountNumber:required}/recent", static async (IGrainFactory grains, string accountNumber) =>
{
    var account = grains.GetGrain<IBankAccountGrain>(accountNumber);

    var recent = await account.Recent();

    return recent;
});

app.Run();
