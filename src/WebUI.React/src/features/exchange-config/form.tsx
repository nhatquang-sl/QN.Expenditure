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
import { ExchangeConfigDto, ExchangeName, UpsertExchangeConfigCommand } from 'store/api-client';
import { useUpsertExchangeConfig } from './hooks/use-upsert-exchange-config';
import { ExchangeConfigData, ExchangeConfigSchema } from './types';

const EXCHANGE_OPTIONS = [
  new InputOption(ExchangeName.Binance, 'Binance'),
  new InputOption(ExchangeName.KuCoin, 'KuCoin'),
  new InputOption(ExchangeName.Coinbase, 'Coinbase'),
  new InputOption(ExchangeName.Kraken, 'Kraken'),
  new InputOption(ExchangeName.Bybit, 'Bybit'),
];

interface ExchangeConfigFormProps {
  exchangeConfig?: ExchangeConfigDto;
}

export default function ExchangeConfigForm({ exchangeConfig }: ExchangeConfigFormProps) {
  const { mutateAsync: upsertConfig } = useUpsertExchangeConfig();

  // Use a key to force form re-render when exchangeConfig changes
  const formKey = useMemo(
    () => exchangeConfig?.exchangeName || 'new',
    [exchangeConfig?.exchangeName]
  );

  const onSubmit = async (data: ExchangeConfigData) => {
    const command = new UpsertExchangeConfigCommand();
    command.exchangeName = data.exchangeName;
    command.apiKey = data.apiKey;
    command.secret = data.secret;
    command.passphrase = data.passphrase || undefined;

    await upsertConfig(command);
  };

  const apiKeyInput = new InputElement('text', 'Api Key', exchangeConfig?.apiKey ?? '');
  const secretInput = new InputElement('text', 'Secret', exchangeConfig?.secret ?? '');
  const passphraseInput = new InputElement('text', 'Passphrase', exchangeConfig?.passphrase ?? '');
  console.log({ exchangeConfig });
  return (
    <Form
      key={formKey}
      onSubmit={onSubmit}
      resolver={zodResolver(ExchangeConfigSchema)}
      blocks={[
        new Block([
          new SelectElement(
            'Exchange Name',
            exchangeConfig?.exchangeName ?? EXCHANGE_OPTIONS[0].value,
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
