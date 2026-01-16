import { useQuery } from '@tanstack/react-query';
import { tradeClient } from 'store';
import type { TradeStatisticsDto } from 'store/api-client';

async function fetchTradeStatistics(symbol: string): Promise<TradeStatisticsDto> {
  return await tradeClient.getTradeStatisticsBySymbol(symbol);
}

export function useGetTradeStatistics(symbol: string) {
  return useQuery({
    queryKey: ['trade', 'statistics', symbol],
    queryFn: () => fetchTradeStatistics(symbol),
    enabled: Boolean(symbol),
  });
}
