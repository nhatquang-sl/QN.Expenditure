import { useQuery } from '@tanstack/react-query';
import { instance } from 'store';
import { API_ENDPOINT } from 'store/constants';
import { SignalStatistics } from '../types';

interface Params {
  interval?: string;
  signalType?: string;
}

export function useGetSignalStatistics(params: Params) {
  return useQuery({
    queryKey: ['signal-statistics', params],
    queryFn: async () => {
      const url = new URL(`${API_ENDPOINT}/api/signals/statistics`);
      if (params.interval) url.searchParams.set('interval', params.interval);
      if (params.signalType) url.searchParams.set('signalType', params.signalType);

      const response = await instance.get<SignalStatistics>(url.toString());
      return response.data;
    },
    staleTime: 30_000,
  });
}
