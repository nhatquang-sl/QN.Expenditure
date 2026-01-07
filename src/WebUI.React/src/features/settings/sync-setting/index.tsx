import { Grid, Paper } from '@mui/material';
import { BackdropLoading } from 'components/backdrop-loading';
import { setTitle } from 'features/layout/slice';
import { useEffect } from 'react';
import { useDispatch } from 'react-redux';
import { Outlet } from 'react-router-dom';
import { useGetSyncSettings } from './hooks/use-get-sync-settings';
import SyncSettingList from './list';

export default function SyncSetting() {
  const dispatch = useDispatch(); 
  const { data: syncSettings, isLoading } = useGetSyncSettings();
  
  useEffect(() => {
    dispatch(setTitle('Sync Settings'));
  }, [dispatch]);

  return (
    <Grid container spacing={3}>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column' }}>
          <Outlet />
          <SyncSettingList syncSettings={syncSettings} />
          <BackdropLoading loading={isLoading} />
        </Paper>
      </Grid>
    </Grid>
  );
}
