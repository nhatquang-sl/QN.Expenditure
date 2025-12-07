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
import { ExchangeName, UpsertExchangeSettingCommand } from 'store/api-client';
import { useUpsertExchangeSetting } from '../hooks/use-upsert-exchange-setting';
import { ExchangeSettingData, ExchangeSettingSchema } from '../types';

const EXCHANGE_OPTIONS = [
  new InputOption(ExchangeName.Binance, 'Binance'),
  new InputOption(ExchangeName.KuCoin, 'KuCoin'),
  new InputOption(ExchangeName.Coinbase, 'Coinbase'),
  new InputOption(ExchangeName.Kraken, 'Kraken'),
  new InputOption(ExchangeName.Bybit, 'Bybit'),
];

export default function ExchangeSettingCreate() {
  const { mutateAsync: upsertSetting } = useUpsertExchangeSetting();

  const onSubmit = async (data: ExchangeSettingData) => {
    const command = new UpsertExchangeSettingCommand();
    command.exchangeName = data.exchangeName;
    command.apiKey = data.apiKey;
    command.secret = data.secret;
    command.passphrase = data.passphrase || undefined;

    await upsertSetting(command);
  };

  const apiKeyInput = new InputElement('text', 'Api Key');
  const secretInput = new InputElement('text', 'Secret');
  const passphraseInput = new InputElement('text', 'Passphrase');

  return (
    <Form
      onSubmit={onSubmit}
      resolver={zodResolver(ExchangeSettingSchema)}
      blocks={[
        new Block([
          new SelectElement('Exchange Name', EXCHANGE_OPTIONS[0].value, EXCHANGE_OPTIONS),
          apiKeyInput,
        ]),
        new Block([secretInput, passphraseInput]),
        new ActionBlock([new ActionElement('Submit', 'submit')]),
      ]}
    />
  );
}
