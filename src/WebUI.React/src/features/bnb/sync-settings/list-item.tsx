import { CircularProgress, Icon, IconButton, TableCell, TableRow } from '@mui/material';
import { MobileDateTimePicker } from '@mui/x-date-pickers';
import dayjs, { Dayjs } from 'dayjs';

import { useState } from 'react';
import { bnbSpotClient } from 'store';

import { SpotOrderSyncSettingDto, SpotOrderSyncSettingUpdateDto } from 'store/api-client';
import { OnChangeCallback } from './types';

const SyncSettingItem = (props: {
  syncSetting: SpotOrderSyncSettingDto;
  onDelete: OnChangeCallback;
}) => {
  const { syncSetting, onDelete } = props;
  const [lastSyncAt, setLastSyncAt] = useState<Dayjs>(dayjs(syncSetting.lastSyncAt));
  const [loading, setLoading] = useState(false);

  const onChangeLastSyncAt = (value: Dayjs | null) => {
    if (value == null) return;
    setLastSyncAt(value);
  };

  const onClose = async () => {
    setLoading(true);

    await bnbSpotClient.updateSyncSetting(
      syncSetting.symbol,
      new SpotOrderSyncSettingUpdateDto({
        lastSyncAt: lastSyncAt.unix() * 1000,
      })
    );

    setLoading(false);
  };

  const handleDelete = async () => {
    setLoading(true);
    await bnbSpotClient.deleteSyncSetting(syncSetting.symbol);
    onDelete(syncSetting);
    setLoading(false);
  };

  return (
    <TableRow
      hover
      role="checkbox"
      tabIndex={-1}
      key={syncSetting.symbol}
      sx={{ position: 'relative' }}
    >
      <TableCell>{syncSetting.symbol}</TableCell>
      <TableCell align="right">
        <MobileDateTimePicker
          value={lastSyncAt}
          onChange={onChangeLastSyncAt}
          onClose={onClose}
          disabled={loading}
        />
      </TableCell>
      <TableCell align="right">
        <IconButton aria-label="delete" onClick={handleDelete} disabled={loading}>
          {loading ? <CircularProgress size={20} color="inherit" /> : <Icon>delete</Icon>}
        </IconButton>
      </TableCell>
    </TableRow>
  );
};

export default SyncSettingItem;
