import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { exchangeSettingsClient } from 'store';
import { ExchangeName } from 'store/api-client';

export const useDeleteExchangeSetting = () => {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  return useMutation({
    mutationFn: (exchangeName: ExchangeName) => exchangeSettingsClient.deleteSetting(exchangeName),
    onSuccess: () => {
      // Invalidate and refetch the exchange settings list
      queryClient.invalidateQueries({ queryKey: ['exchangeSettings'] });
      navigate(`/settings/exchange-setting`);
    },
  });
};
