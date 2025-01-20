using Newtonsoft.Json;
using Orleans.Concurrency;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace OrleansPlayground;

[Alias("IBankAccountGrain")]
public interface IBankAccountGrain : IGrainWithStringKey
{
    [Alias("Deposit")]
    Task Deposit(decimal amount);

    [Alias("Withdraw")]
    Task Withdraw(decimal amount);

    [ReadOnly, Alias("GetBalance")]
    Task<decimal> GetBalance();

    // alias promotes seamless C# idioms, without worrying about Orleans routing
    // see serialization best practices, point 1
    // https://learn.microsoft.com/en-us/dotnet/orleans/host/configuration-guide/serialization?pivots=orleans-7-0#serialization-best-practices
    [ReadOnly, Alias("GetBalanceAtPointInTime")]
    Task<decimal> GetBalance(DateTimeOffset timestamp);

    [ReadOnly, Alias("Recent")]
    Task<IEnumerable<RecentTransaction>> Recent();
}

[GenerateSerializer]
[Alias("RecentTransaction")]
public sealed class RecentTransaction
{
    [Id(0)]
    public required string Method { get; set; }

    [Id(1)]
    public required decimal Amount { get; set; }

    [Id(2)]
    public required DateTimeOffset Timestamp { get; set; }
}

// there's no need to generate serializers for any of the state classes below
// unless we swap the default newtonsoft serializer with the Orleans binary serializer
// if we do swap the serializer and forget the attributes, Orleans will freak out at runtime!
public sealed class BankAccountState
{
    public decimal Balance { get; set; }

    public void Apply(Deposit deposit)
    {
        Balance += deposit.Amount;
    }

    public void Apply(Withdraw withdraw)
    {
        Balance -= withdraw.Amount;
    }
}

public sealed record Deposit(decimal Amount) : Transaction(Amount)
{
    public override string Name => "deposit";
}

public sealed record Withdraw(decimal Amount) : Transaction(Amount)
{
    public override string Name => "withdrawal";
}

public abstract record Transaction(decimal Amount)
{
    [JsonIgnore]
    public abstract string Name { get; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

[StorageProvider(ProviderName = "accounts"), LogConsistencyProvider(ProviderName = "LogStorage")]
public sealed class BankAccountGrain : JournaledGrain<BankAccountState, Transaction>, IBankAccountGrain
{
    public async Task Deposit(decimal amount)
    {
        RaiseEvent(new Deposit(amount));

        await ConfirmEvents();
    }

    public async Task Withdraw(decimal amount)
    {
        RaiseEvent(new Withdraw(amount));

        await ConfirmEvents();
    }

    public Task<decimal> GetBalance()
    {
        return Task.FromResult(State.Balance);
    }

    public async Task<decimal> GetBalance(DateTimeOffset timestamp)
    {
        var state = new BankAccountState();

        var transactions = await RetrieveConfirmedEvents(0, Version);

        foreach (dynamic transaction in transactions.Where(transaction => transaction.Timestamp < timestamp))
        {
            state.Apply(transaction);
        }

        return state.Balance;
    }

    public async Task<IEnumerable<RecentTransaction>> Recent()
    {
        var from = Math.Max(0, Version - 10);

        var transactions = await RetrieveConfirmedEvents(from, Version);

        var mapped = transactions.Select(static transaction => new RecentTransaction
        {
            Amount = transaction.Amount,
            Method = transaction.Name,
            Timestamp = transaction.Timestamp,
        })

        // if you comment this, you will get a codec not found error!
        // this is because the Orleans serializer doesn't support IEnumerable
        // it can however serialize lists, arrays, etc.
        .ToList()

        ;

        return mapped;
    }
}
