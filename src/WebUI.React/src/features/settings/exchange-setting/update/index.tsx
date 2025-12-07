import { BackdropLoading } from 'components/backdrop-loading';
import { useParams } from 'react-router-dom';
import { ExchangeName } from 'store/api-client';
import ExchangeSettingForm from '../form';
import { useGetExchangeSettings } from '../hooks/use-get-exchange-settings';

export default function ExchangeSettingUpdate() {
  const { exchangeName } = useParams<{ exchangeName: string }>();
  const { data: exchangeSettings, isLoading } = useGetExchangeSettings();
  if (isLoading) {
    return <BackdropLoading loading={true} />;
  }

  const exchangeSetting = exchangeSettings?.find(
    (setting) => setting.exchangeName === (parseInt(exchangeName || '') as ExchangeName)
  );

  return <ExchangeSettingForm exchangeSetting={exchangeSetting} />;
}
