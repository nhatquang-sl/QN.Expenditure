import { useQuery } from '@tanstack/react-query';
import { tradeClient } from 'store';

export function useGetTradeHistories(symbol: string, pageNumber: number, pageSize: number) {
  return useQuery({
    queryKey: ['trade-histories', symbol, pageNumber, pageSize],
    queryFn: async () => await tradeClient.getTradeHistoriesBySymbol(symbol, pageNumber, pageSize),
    enabled: !!symbol,
  });
}
