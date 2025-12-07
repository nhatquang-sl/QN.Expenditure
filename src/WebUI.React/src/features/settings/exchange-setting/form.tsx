import { zodResolver } from '@hookform/resolvers/zod';
import Form from 'components/form';
import {
  ActionBlock,
  ActionElement,
  Block,
  InputElement,
  InputOption,
  SelectElement,
} from 'components/form/types';
import { useMemo } from 'react';
import { ExchangeName, ExchangeSettingDto, UpsertExchangeSettingCommand } from 'store/api-client';
import { useUpsertExchangeSetting } from './hooks/use-upsert-exchange-setting';
import { ExchangeSettingData, ExchangeSettingSchema } from './types';

const EXCHANGE_OPTIONS = [
  new InputOption(ExchangeName.Binance, 'Binance'),
  new InputOption(ExchangeName.KuCoin, 'KuCoin'),
  new InputOption(ExchangeName.Coinbase, 'Coinbase'),
  new InputOption(ExchangeName.Kraken, 'Kraken'),
  new InputOption(ExchangeName.Bybit, 'Bybit'),
];

interface ExchangeSettingFormProps {
  exchangeSetting?: ExchangeSettingDto;
}

export default function ExchangeSettingForm({ exchangeSetting }: ExchangeSettingFormProps) {
  const { mutateAsync: upsertSetting } = useUpsertExchangeSetting();

  // Use a key to force form re-render when exchangeSetting changes
  const formKey = useMemo(
    () => exchangeSetting?.exchangeName || 'new',
    [exchangeSetting?.exchangeName]
  );

  const onSubmit = async (data: ExchangeSettingData) => {
    const command = new UpsertExchangeSettingCommand();
    command.exchangeName = data.exchangeName;
    command.apiKey = data.apiKey;
    command.secret = data.secret;
    command.passphrase = data.passphrase || undefined;

    await upsertSetting(command);
  };

  const apiKeyInput = new InputElement('text', 'Api Key', exchangeSetting?.apiKey ?? '');
  const secretInput = new InputElement('text', 'Secret', exchangeSetting?.secret ?? '');
  const passphraseInput = new InputElement('text', 'Passphrase', exchangeSetting?.passphrase ?? '');

  return (
    <Form
      key={formKey}
      onSubmit={onSubmit}
      resolver={zodResolver(ExchangeSettingSchema)}
      blocks={[
        new Block([
          new SelectElement(
            'Exchange Name',
            exchangeSetting?.exchangeName ?? EXCHANGE_OPTIONS[0].value,
            EXCHANGE_OPTIONS
          ),
          apiKeyInput,
        ]),
        new Block([secretInput, passphraseInput]),
        new ActionBlock([new ActionElement('Submit', 'submit')]),
      ]}
    />
  );
}
