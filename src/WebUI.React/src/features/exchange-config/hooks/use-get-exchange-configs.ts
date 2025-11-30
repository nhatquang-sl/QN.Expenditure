import { useQuery } from '@tanstack/react-query';
import { exchangeConfigsClient } from 'store';

export const useGetExchangeConfigs = () => {
  return useQuery({
    queryKey: ['exchangeConfigs'],
    queryFn: () => exchangeConfigsClient.getConfigs(),
  });
};
