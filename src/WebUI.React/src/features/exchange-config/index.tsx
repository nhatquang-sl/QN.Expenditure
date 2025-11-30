import { Grid, Paper, Typography } from '@mui/material';
import { BackdropLoading } from 'components/backdrop-loading';
import { Outlet } from 'react-router-dom';
import { useGetExchangeConfigs } from './hooks/use-get-exchange-configs';
import ExchangeConfigList from './list';

export default function ExchangeConfig() {
  const { data: exchangeConfigs, isLoading } = useGetExchangeConfigs();

  return (
    <Grid container spacing={3}>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column' }}>
          <Typography component="h2" variant="h6" color="primary" gutterBottom>
            Bnb Setting
          </Typography>
          <Outlet />
          <ExchangeConfigList exchangeConfigs={exchangeConfigs} />
          <BackdropLoading loading={isLoading} />
        </Paper>
      </Grid>
    </Grid>
  );
}
