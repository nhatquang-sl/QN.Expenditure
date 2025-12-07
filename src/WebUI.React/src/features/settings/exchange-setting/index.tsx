import { Grid, Paper, Typography } from '@mui/material';
import { BackdropLoading } from 'components/backdrop-loading';
import { Outlet } from 'react-router-dom';
import { useGetExchangeSettings } from './hooks/use-get-exchange-settings';
import ExchangeSettingList from './list';

export default function ExchangeSetting() {
  const { data: exchangeSettings, isLoading } = useGetExchangeSettings();

  return (
    <Grid container spacing={3}>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column' }}>
          <Typography component="h2" variant="h6" color="primary" gutterBottom>
            Exchange Settings
          </Typography>
          <Outlet />
          <ExchangeSettingList exchangeSettings={exchangeSettings} />
          <BackdropLoading loading={isLoading} />
        </Paper>
      </Grid>
    </Grid>
  );
}
