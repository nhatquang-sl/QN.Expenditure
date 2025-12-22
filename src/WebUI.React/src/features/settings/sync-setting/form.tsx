import { zodResolver } from '@hookform/resolvers/zod';
import Form from 'components/form';

import { DateTimeElement, SelectElement } from 'components/form/elements';
import { ActionBlock, ActionElement, Block, InputOption } from 'components/form/types';
import dayjs from 'dayjs';
import { useMemo } from 'react';
import { SyncSettingDto, UpsertSyncSettingCommand } from 'store/api-client';
import { useUpsertSyncSetting } from './hooks/use-upsert-sync-setting';
import { SyncSettingData, SyncSettingSchema } from './types';

interface SyncSettingFormProps {
  syncSetting?: SyncSettingDto;
}

export default function SyncSettingForm({ syncSetting }: SyncSettingFormProps) {
  const { mutateAsync: upsertSetting } = useUpsertSyncSetting();

  const formKey = useMemo(() => syncSetting?.symbol || 'new', [syncSetting?.symbol]);

  const onSubmit = async (data: SyncSettingData) => {
    console.log('Submitting data:', data);
    const command = new UpsertSyncSettingCommand();
    command.symbol = data.symbol;
    // Convert Dayjs to Unix timestamp (milliseconds) before sending to server
    command.startSync = data.startSync.valueOf();

    await upsertSetting(command);
  };

  const SYMBOLS = [
    new InputOption('XAUT-USDT'),
    new InputOption('KCS-USDT'),
    new InputOption('BTC-USDT'),
    new InputOption('ETH-USDT'),
    new InputOption('BNB-USDT'),
    new InputOption('LTC-USDT'),
  ];

  const symbolInput = new SelectElement('Symbol', syncSetting?.symbol ?? SYMBOLS[0].value, SYMBOLS);
  const startSyncInput = new DateTimeElement(
    'Start Sync',
    syncSetting?.startSync
      ? dayjs(syncSetting.startSync) // Convert Unix timestamp (milliseconds) to Dayjs
      : dayjs().subtract(6, 'month')
  );
  startSyncInput.flex = 'none';

  const blocks = [new Block([symbolInput, startSyncInput])];

  // Show Last Sync as read-only in update mode
  //   if (syncSetting) {
  //     const lastSyncInput = new DateTimeElement(
  //       'Last Sync',
  //       dayjs(syncSetting.lastSync) // Convert Unix timestamp (milliseconds) to Dayjs
  //     );
  //     blocks.push(new Block([lastSyncInput]));
  //   }

  blocks.push(new ActionBlock([new ActionElement('Submit', 'submit')]));

  return (
    <Form
      key={formKey}
      onSubmit={onSubmit}
      resolver={zodResolver(SyncSettingSchema)}
      blocks={blocks}
    />
  );
}
