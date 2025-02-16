import {
  FormControl,
  Grid,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  SelectChangeEvent,
  Typography,
} from '@mui/material';

import { useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { Outlet } from 'react-router-dom';
import { RootState } from 'store';
import Chart from './chart';
import { INTERVALS, setInterval, setSymbol, SYMBOLS } from './slice';
import { fixedNumber } from './utils';

export default function SpotGrid() {
  const dispatch = useDispatch();
  const { symbol, interval } = useSelector((state: RootState) => state.spotGrid);

  const [curPrice, setCurPrice] = useState(0);
  useEffect(() => {
    // WS: get market price
    const markPriceWS = new WebSocket(
      `wss://stream.binance.com:9443/ws/${symbol.toLowerCase()}@kline_1h`
    );
    markPriceWS.onmessage = function (event) {
      try {
        const json = JSON.parse(event.data);
        setCurPrice(fixedNumber(parseFloat(json.k.c)));
      } catch (err) {
        console.error(err);
      }
    };

    return () => markPriceWS.close();
  }, [symbol]);

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
          <Typography>{curPrice}</Typography>
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
