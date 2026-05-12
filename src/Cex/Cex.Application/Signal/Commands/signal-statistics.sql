SELECT TOP (1000)
      [Interval]
      ,[SignalType]
      ,[EntryPrice]
      ,[StopLoss]
      ,[DetectedAt]
      ,[EntryHitAt]
      ,[MaxProfitCheckedAt]
      ,[StopLossHitAt]
      ,[MaxProfitHitAt]
      ,[MaxProfit]
      ,DATEDIFF(MINUTE, [DetectedAt], [EntryHitAt])    AS [EntryHitMinutes]
      ,DATEDIFF(MINUTE, [DetectedAt], [StopLossHitAt]) AS [StopLossHitMinutes]
      ,DATEDIFF(MINUTE, [DetectedAt], [MaxProfitHitAt]) AS [MaxProfitHitMinutes]
  FROM [cex].[dbo].[Signals]
--   WHERE StopLossHitAt IS NOT NULL



  Update [cex].[dbo].[Signals]
  SET MaxProfitCheckedAt = EntryHitAt, MaxProfit = 0, MaxProfitHitAt = NULL
  WHERE EntryHitAt IS NOT NULL
  