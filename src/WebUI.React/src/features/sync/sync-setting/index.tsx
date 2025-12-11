import { Grid, Paper, Typography } from '@mui/material';
import { BackdropLoading } from 'components/backdrop-loading';
import { Outlet } from 'react-router-dom';
import { useGetSyncSettings } from './hooks/use-get-sync-settings';
import SyncSettingList from './list';

export default function SyncSetting() {
  const { data: syncSettings, isLoading } = useGetSyncSettings();

  return (
    <Grid container spacing={3}>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column' }}>
          <Typography component="h2" variant="h6" color="primary" gutterBottom>
            Sync Settings
          </Typography>
          <Outlet />
          <SyncSettingList syncSettings={syncSettings} />
          <BackdropLoading loading={isLoading} />
        </Paper>
      </Grid>
    </Grid>
  );
}
