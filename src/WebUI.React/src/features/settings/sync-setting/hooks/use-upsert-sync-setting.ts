import { useMutation, useQueryClient } from '@tanstack/react-query';
import { syncSettingsClient } from 'store';
import { UpsertSyncSettingCommand } from 'store/api-client';

export const useUpsertSyncSetting = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpsertSyncSettingCommand) => syncSettingsClient.upsertSetting(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['syncSettings'] });
    },
  });
};
