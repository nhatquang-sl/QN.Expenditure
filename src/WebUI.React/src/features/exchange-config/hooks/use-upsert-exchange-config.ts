import { useMutation, useQueryClient } from '@tanstack/react-query';
import { exchangeConfigsClient } from 'store';
import { UpsertExchangeConfigCommand } from 'store/api-client';

export const useUpsertExchangeConfig = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpsertExchangeConfigCommand) => exchangeConfigsClient.upsertConfig(data),
    onSuccess: () => {
      // Invalidate and refetch the exchange configs list
      queryClient.invalidateQueries({ queryKey: ['exchangeConfigs'] });
    },
  });
};
