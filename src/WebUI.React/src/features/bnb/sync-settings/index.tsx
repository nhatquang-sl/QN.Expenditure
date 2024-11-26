import {
  Grid,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { LocalizationProvider } from '@mui/x-date-pickers';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { BackdropLoading } from 'components/backdrop-loading';
import { useCallback, useEffect, useState } from 'react';
import { bnbSpotClient } from 'store';
import { SpotOrderSyncSettingDto } from 'store/api-client';
import AddSyncSetting from './add';
import SyncSettingItem from './list-item';
import { columns } from './types';

const BnbSpotOrdersSyncSettings = () => {
  const [loading, setLoading] = useState(false);
  const [syncSettings, setSyncSettings] = useState<SpotOrderSyncSettingDto[]>([]);

  const fetchSessions = useCallback(async () => {
    setLoading(true);
    const syncSettings = await bnbSpotClient.getSyncSettings();
    setSyncSettings(syncSettings);
    console.log(syncSettings);
    setLoading(false);
  }, []);

  useEffect(() => {
    console.log('LoginHistory');

    fetchSessions();
  }, [fetchSessions]);

  const addNewSyncSettingCallback = (syncSetting: SpotOrderSyncSettingDto) => {
    setSyncSettings(syncSettings.concat(syncSetting));
  };

  const deleteSyncSettingCallback = (syncSetting: SpotOrderSyncSettingDto) => {
    setSyncSettings(syncSettings.filter((x) => x.symbol !== syncSetting.symbol));
  };

  return (
    <LocalizationProvider dateAdapter={AdapterDayjs}>
      <Grid container spacing={3}>
        <Grid item xs={12}>
          <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column', position: 'relative' }}>
            <TableContainer sx={{ flex: 1 }}>
              <Typography component="h2" variant="h6" color="primary" gutterBottom>
                Sync Settings
              </Typography>

              <AddSyncSetting onAddNew={addNewSyncSettingCallback}></AddSyncSetting>
              <Table stickyHeader aria-label="sticky table">
                <TableHead>
                  <TableRow>
                    {columns.map((column) => (
                      <TableCell
                        key={column.id}
                        align={column.align}
                        style={{ width: column.minWidth }}
                      >
                        {column.label}
                      </TableCell>
                    ))}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {syncSettings &&
                    syncSettings.map((syncSetting) => {
                      return (
                        <SyncSettingItem
                          key={syncSetting.symbol}
                          syncSetting={syncSetting}
                          onDelete={deleteSyncSettingCallback}
                        />
                      );
                    })}
                </TableBody>
              </Table>
            </TableContainer>
            <BackdropLoading loading={loading} />
          </Paper>
        </Grid>
      </Grid>
    </LocalizationProvider>
  );
};

export default BnbSpotOrdersSyncSettings;
