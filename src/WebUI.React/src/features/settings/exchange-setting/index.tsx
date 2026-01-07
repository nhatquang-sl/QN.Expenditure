import { Grid, Paper } from '@mui/material';
import { BackdropLoading } from 'components/backdrop-loading';
import { setTitle } from 'features/layout/slice';
import { useEffect } from 'react';
import { useDispatch } from 'react-redux';
import { Outlet } from 'react-router-dom';
import { useGetExchangeSettings } from './hooks/use-get-exchange-settings';
import ExchangeSettingList from './list';

export default function ExchangeSetting() {
  const { data: exchangeSettings, isLoading } = useGetExchangeSettings();
  const dispatch = useDispatch(); 
  
  useEffect(() => {
    dispatch(setTitle('Exchange Settings'));
  }, [dispatch]);

  return (
    <Grid container spacing={3}>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column' }}>
          <Outlet />
          <ExchangeSettingList exchangeSettings={exchangeSettings} />
          <BackdropLoading loading={isLoading} />
        </Paper>
      </Grid>
    </Grid>
  );
}
