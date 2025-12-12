import { useMutation, useQueryClient } from '@tanstack/react-query';
import { exchangeSettingsClient } from 'store';
import { UpsertExchangeSettingCommand } from 'store/api-client';

export const useUpsertExchangeSetting = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpsertExchangeSettingCommand) => exchangeSettingsClient.upsertSetting(data),
    onSuccess: () => {
      // Invalidate and refetch the exchange settings list
      queryClient.invalidateQueries({ queryKey: ['exchangeSettings'] });
    },
  });
};
