import {
  FormControl,
  Grid,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  SelectChangeEvent,
} from '@mui/material';

import { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { Outlet } from 'react-router-dom';
import { RootState } from 'store';
import Chart from './chart';
import CurrentPrice from './components/current-price';
import { INTERVALS, SYMBOLS, setInterval, setPrice, setSymbol } from './slice';
import { fixedNumber } from './utils';

export default function SpotGrid() {
  const dispatch = useDispatch();
  const { symbol, interval } = useSelector((state: RootState) => state.spotGrid);

  useEffect(() => {
    // WS: get market price
    const markPriceWS = new WebSocket(
      `wss://stream.binance.com:9443/ws/${symbol}@kline_${interval}`.toLowerCase()
    );
    markPriceWS.onmessage = function (event) {
      try {
        const json = JSON.parse(event.data);
        const curPrice = fixedNumber(Number(json.k.c));
        dispatch(setPrice([symbol, curPrice]));
      } catch (err) {
        console.error(err);
      }
    };

    return () => markPriceWS.close();
  }, [symbol, interval, dispatch]);

  const handleChange = (event: SelectChangeEvent) => {
    console.log(event.target);
    switch (event.target.name) {
      case 'chart-symbol-select':
        dispatch(setSymbol(event.target.value));
        break;
      case 'chart-interval-select':
        dispatch(setInterval(event.target.value));
        break;
    }
  };

  return (
    <Grid container>
      <Grid item xs={12}>
        <Paper
          elevation={0}
          sx={{ p: 2, position: 'relative', display: 'flex', alignItems: 'center' }}
        >
          <FormControl sx={{ mr: 1, minWidth: 80 }} size="small">
            <InputLabel>Symbol</InputLabel>
            <Select
              name="chart-symbol-select"
              value={symbol}
              onChange={handleChange}
              autoWidth
              label="Symbol"
            >
              {SYMBOLS.map((option) => (
                <MenuItem key={option} value={option}>
                  {option}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <FormControl sx={{ mr: 1, minWidth: 80 }} size="small">
            <InputLabel>Interval</InputLabel>
            <Select
              name="chart-interval-select"
              value={interval}
              onChange={handleChange}
              autoWidth
              label="Interval"
            >
              {INTERVALS.map((option) => (
                <MenuItem key={option} value={option}>
                  {option}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <CurrentPrice symbol={symbol} />
        </Paper>
      </Grid>
      <Grid item xs={12}>
        <Chart pair={symbol} interval={interval}></Chart>
      </Grid>
      <Grid item xs={12}>
        <Outlet />
      </Grid>
    </Grid>
  );
}
