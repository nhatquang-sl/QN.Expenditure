import { useQuery } from '@tanstack/react-query';
import { instance } from 'store';
import { API_ENDPOINT } from 'store/constants';
import { PaginatedList, SignalDto } from '../types';

interface Params {
  from: string;
  to: string;
  interval?: string;
  signalType?: string;
  pageNumber: number;
  pageSize: number;
}

export function useGetSignals(params: Params) {
  return useQuery({
    queryKey: ['signals', params],
    queryFn: async () => {
      const url = new URL(`${API_ENDPOINT}/api/signals`);
      url.searchParams.set('from', params.from);
      url.searchParams.set('to', params.to);
      if (params.interval) url.searchParams.set('interval', params.interval);
      if (params.signalType) url.searchParams.set('signalType', params.signalType);
      url.searchParams.set('pageNumber', String(params.pageNumber));
      url.searchParams.set('pageSize', String(params.pageSize));

      const response = await instance.get<PaginatedList<SignalDto>>(url.toString());
      return response.data;
    },
  });
}
