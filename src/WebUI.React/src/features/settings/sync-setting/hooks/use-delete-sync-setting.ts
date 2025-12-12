import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { syncSettingsClient } from 'store';

export const useDeleteSyncSetting = () => {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  return useMutation({
    mutationFn: (symbol: string) => syncSettingsClient.deleteSetting(symbol),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['syncSettings'] });
      navigate(`/sync/sync-setting`);
    },
  });
};
