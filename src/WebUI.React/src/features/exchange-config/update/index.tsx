import { BackdropLoading } from 'components/backdrop-loading';
import { useParams } from 'react-router-dom';
import { ExchangeName } from 'store/api-client';
import ExchangeConfigForm from '../form';
import { useGetExchangeConfigs } from '../hooks/use-get-exchange-configs';

export default function ExchangeConfigUpdate() {
  const { exchangeName } = useParams<{ exchangeName: string }>();
  const { data: exchangeConfigs, isLoading } = useGetExchangeConfigs();
  if (isLoading) {
    return <BackdropLoading loading={true} />;
  }

  const exchangeConfig = exchangeConfigs?.find(
    (config) => config.exchangeName === (parseInt(exchangeName || '') as ExchangeName)
  );
  console.log({ exchangeName, exchangeConfigs, isLoading, exchangeConfig });

  return <ExchangeConfigForm exchangeConfig={exchangeConfig} />;
}
