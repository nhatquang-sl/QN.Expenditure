Base on the Signals data,
I would like to have an signals statistics api return the SignalStatistics object

```csharp
class SignalStatisticInfo {
    public int TotalSignals { get; set; }
    public int TotalEntries { get; set; }
    public int TotalMaxProfit { get; set; }
    public int TotalStoploss { get; set; }

    public decimal AvgEntryPrice { get; set; }
    public decimal AvgMaxProfit { get; set; }
}

```

```csharp
class SignalStatistics {
    public StatisticInfo Today { get; set; }
    public StatisticInfo Yesterday { get; set; }
    public StatisticInfo ThisWeek { get; set; }
    public StatisticInfo LastWeek { get; set; }
    public StatisticInfo ThisMonth { get; set; }
    public StatisticInfo LastMonth { get; set; }
}
```

The frontend should display those data in the /signals, it should be a new component that display the statistic data and located below of SignalSearchBar.
