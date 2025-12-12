import {
  FormControl,
  Grid,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  SelectChangeEvent,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { BackdropLoading } from 'components/backdrop-loading';
import { useCallback, useEffect, useState } from 'react';
import { bnbSpotClient } from 'store';
import { SpotOrderRaw } from 'store/api-client';
import { columns, summaryColumns } from './types';

class OrderSummary {
  constructor(symbol: string) {
    this.symbol = symbol;
  }
  symbol: string = '';
  buy: number = 0;
  buyAvgPrice: number = 0;
  buyNew: number = 0;
  sell: number = 0;
  sellNew: number = 0;
  cumBuy: number = 0;
  cumBuyNew: number = 0;
  cumSell: number = 0;
  profit: number = 0;
  lastSyncAt: number = 0;
}

const ACCEPTED_STATUS = ['NEW', 'FILLED'];

export default function BnbSpotOrders() {
  const [loading, setLoading] = useState(false);
  const [spotOrders, setSpotOrders] = useState<SpotOrderRaw[]>([]);
  const [summaries, setSummaries] = useState<OrderSummary[]>([]);
  const [age, setAge] = useState('');

  const fetchSessions = useCallback(async () => {
    setLoading(true);
    const spotOrders = await bnbSpotClient.getSpotOrders();

    const symbolMap = new Map<string, number>();
    const summaries: OrderSummary[] = [];

    spotOrders.forEach((order) => {
      if (!symbolMap.has(order.symbol)) {
        symbolMap.set(order.symbol, summaries.length);
        summaries.push(new OrderSummary(order.symbol));
      }
      const summary = summaries[symbolMap.get(order.symbol) ?? 0];
      switch (`${order.side}_${order.status}`) {
        case 'BUY_FILLED':
          summary.buy += parseFloat(order.executedQty);
          summary.cumBuy += parseFloat(order.cummulativeQuoteQty);
          break;
        case 'BUY_NEW':
          summary.buyNew += parseFloat(order.origQty);
          break;
        case 'SELL_NEW':
          summary.sellNew += parseFloat(order.origQty);
          break;
        case 'SELL_FILLED':
          summary.sell += parseFloat(order.executedQty);
          summary.cumSell += parseFloat(order.cummulativeQuoteQty);
          break;
      }

      if (summary.lastSyncAt < order.updateTime) summary.lastSyncAt = order.updateTime;
    });

    summaries.forEach((s) => {
      s.profit = s.cumSell - s.cumBuy;
      s.buyAvgPrice = s.cumBuy / s.buy;
    });
    setSummaries(summaries);
    console.log(summaries);
    setSpotOrders(spotOrders);

    setLoading(false);
  }, []);

  const handleChange = (event: SelectChangeEvent) => {
    setAge(event.target.value);
  };

  useEffect(() => {
    fetchSessions();
  }, [fetchSessions]);
  return (
    <Grid container spacing={3}>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column', position: 'relative' }}>
          <TableContainer sx={{ flex: 1 }}>
            <Typography component="h2" variant="h6" color="primary" gutterBottom>
              Total Summary
            </Typography>

            <Table stickyHeader aria-label="sticky table">
              <TableHead>
                <TableRow>
                  {summaryColumns.map((column) => (
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
                {summaries.map((s) => (
                  <TableRow key={s.symbol}>
                    {summaryColumns.map((col) => {
                      const value = s[col.id];
                      return (
                        <TableCell key={col.id}>
                          {col.format ? col.format(value) : value?.toString()}
                        </TableCell>
                      );
                    })}
                  </TableRow>
                ))}
                {/* <TableRow> 
                  <TableCell>{total.buy}</TableCell>
                  <TableCell>{total.sell}</TableCell>
                  <TableCell>{total.cumBuy}</TableCell>
                  <TableCell>{total.cumSell}</TableCell>
                  <TableCell>{round4Dec(total.cumSell - total.cumBuy)}</TableCell>
                  <TableCell>{new Date(total.lastSyncAt).toISOString()}</TableCell>
                </TableRow> */}
              </TableBody>
            </Table>
          </TableContainer>
          <BackdropLoading loading={loading} />
        </Paper>
      </Grid>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column', position: 'relative' }}>
          <TableContainer sx={{ flex: 1 }}>
            <Typography component="h2" variant="h6" color="primary" gutterBottom>
              Histories
            </Typography>

            <FormControl sx={{ m: 1, minWidth: 120 }} size="small">
              <InputLabel>Symbol</InputLabel>
              <Select value={age} label="Symbol" onChange={handleChange}>
                {[...new Set(spotOrders.map((x) => x.symbol))].map((x) => (
                  <MenuItem key={x} value={x}>
                    {x}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

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
                {spotOrders
                  .filter(
                    (order) =>
                      ACCEPTED_STATUS.indexOf(order.status) >= 0 &&
                      (age == '' || order.symbol === age)
                  )
                  .map((order) => (
                    <TableRow key={order.orderId}>
                      {columns.map((col) => {
                        const value = order[col.id];
                        return (
                          <TableCell key={col.id}>
                            {col.format ? col.format(value ?? '') : value?.toString()}
                          </TableCell>
                        );
                      })}
                    </TableRow>
                  ))}
              </TableBody>
            </Table>
          </TableContainer>
          <BackdropLoading loading={loading} />
        </Paper>
      </Grid>
    </Grid>
  );
}
