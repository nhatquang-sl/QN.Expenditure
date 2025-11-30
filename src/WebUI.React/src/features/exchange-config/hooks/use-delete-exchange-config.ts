import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { exchangeConfigsClient } from 'store';
import { ExchangeName } from 'store/api-client';

export const useDeleteExchangeConfig = () => {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  return useMutation({
    mutationFn: (exchangeName: ExchangeName) => exchangeConfigsClient.deleteConfig(exchangeName),
    onSuccess: () => {
      // Invalidate and refetch the exchange configs list
      queryClient.invalidateQueries({ queryKey: ['exchangeConfigs'] });
      navigate(`/exchange-config`);
    },
  });
};
