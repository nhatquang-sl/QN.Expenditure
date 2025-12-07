import { useQuery } from '@tanstack/react-query';
import { exchangeSettingsClient } from 'store';

export const useGetExchangeSettings = () => {
  return useQuery({
    queryKey: ['exchangeSettings'],
    queryFn: () => exchangeSettingsClient.getSettings(),
  });
};
