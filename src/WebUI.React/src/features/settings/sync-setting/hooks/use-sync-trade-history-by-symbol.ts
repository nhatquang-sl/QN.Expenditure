import { useMutation, useQueryClient } from '@tanstack/react-query';
import { showSnackbar } from 'components/snackbar/slice';
import { useDispatch } from 'react-redux';
import { tradeClient } from 'store';
import { ApiException } from 'store/api-client';

interface SyncResult {
  totalSynced: number;
  totalBuy: number;
  totalSell: number;
  profit: number;
}

export function useSyncTradeHistoryBySymbol() {
  const queryClient = useQueryClient();
  const dispatch = useDispatch();

  return useMutation({
    mutationFn: (symbol: string) => tradeClient.syncTradeHistoryBySymbol(symbol),
    onSuccess: (result: SyncResult) => {
      dispatch(showSnackbar(`Successfully synced ${result.totalSynced} trades.`, 'success'));
      queryClient.invalidateQueries({ queryKey: ['sync-settings'] });
    },
    onError: (error: ApiException) => {
      const errorMessage = error?.message || 'Failed to sync trade history';
      dispatch(showSnackbar(errorMessage, 'error'));
    },
  });
}
