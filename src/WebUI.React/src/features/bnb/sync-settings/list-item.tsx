import { CircularProgress, Icon, IconButton, TableCell, TableRow } from '@mui/material';
import { MobileDateTimePicker } from '@mui/x-date-pickers';
import dayjs, { Dayjs } from 'dayjs';

import { useState } from 'react';
import { bnbSpotClient } from 'store';

import { showSnackbar } from 'components/snackbar/slice';
import { useDispatch } from 'react-redux';
import {
  BadRequest,
  SpotOrderSyncSettingDto,
  SpotOrderSyncSettingUpdateDto,
} from 'store/api-client';
import { OnChangeCallback } from './types';

const SyncSettingItem = (props: {
  syncSetting: SpotOrderSyncSettingDto;
  onDelete: OnChangeCallback;
}) => {
  const { syncSetting, onDelete } = props;
  const dispatch = useDispatch();
  const [lastSyncAt, setLastSyncAt] = useState<Dayjs>(dayjs(syncSetting.lastSyncAt));
  const [loading, setLoading] = useState(false);

  const onChangeLastSyncAt = (value: Dayjs | null) => {
    if (value == null) return;
    setLastSyncAt(value);
  };

  const onClose = async () => {
    setLoading(true);

    try {
      await bnbSpotClient.updateSyncSetting(syncSetting.symbol, {
        lastSyncAt: lastSyncAt.unix() * 1000,
      } as SpotOrderSyncSettingUpdateDto);
    } catch (err: any) {
      setLastSyncAt(dayjs(syncSetting.lastSyncAt));
      if (err instanceof BadRequest) {
        dispatch(showSnackbar(err.message, 'error', 'top', 'right'));
      }
    }

    setLoading(false);
  };

  const handleDelete = async () => {
    setLoading(true);
    await bnbSpotClient.deleteSyncSetting(syncSetting.symbol);
    onDelete(syncSetting);
    setLoading(false);
  };

  const handleSync = async () => {
    setLoading(true);
    try {
      var res = await bnbSpotClient.triggerSync(syncSetting.symbol);
      setLastSyncAt(dayjs(res.lastSyncAt));
    } catch (err) {}
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
        {loading ? (
          <CircularProgress size={20} color="inherit" />
        ) : (
          <>
            <IconButton aria-label="delete" onClick={handleDelete} disabled={loading}>
              <Icon>delete</Icon>
            </IconButton>
            <IconButton aria-label="delete" onClick={handleSync} disabled={loading}>
              <Icon>sync</Icon>
            </IconButton>
          </>
        )}
      </TableCell>
    </TableRow>
  );
};

export default SyncSettingItem;
