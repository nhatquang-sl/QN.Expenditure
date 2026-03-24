import { useQuery } from '@tanstack/react-query';
import { useDispatch } from 'react-redux';
import { authClient } from 'store';
import { setAuth } from '../slice';

export function useInitAuth() {
  const dispatch = useDispatch();

  return useQuery({
    queryKey: ['auth', 'profile'],
    queryFn: () => authClient.check(),
    retry: false,
    staleTime: Infinity,
    select: (data) => {
      dispatch(setAuth(data));
      return data;
    },
  });
}
