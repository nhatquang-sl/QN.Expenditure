import { useQuery } from '@tanstack/react-query';
import { syncSettingsClient } from 'store';

export const useGetSyncSettings = () => {
  return useQuery({
    queryKey: ['syncSettings'],
    queryFn: () => syncSettingsClient.getSettings(),
  });
};
