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
import { BackdropLoading } from 'components/backdrop-loading';
import { useCallback, useEffect, useState } from 'react';
import { bnbSpotClient } from 'store';
import { SpotOrderRaw } from 'store/api-client';
import { round4Dec } from 'store/constants';
import { columns } from './types';

export default function BnbSpotOrders() {
  const [loading, setLoading] = useState(false);
  const [spotOrders, setSpotOrders] = useState<SpotOrderRaw[]>([]);
  const [total, setTotal] = useState<{
    buy: number;
    sell: number;
    cumBuy: number;
    cumSell: number;
  }>({ buy: 0, sell: 0, cumBuy: 0, cumSell: 0 });

  const fetchSessions = useCallback(async () => {
    setLoading(true);
    var spotOrders = await bnbSpotClient.getSpotOrders();
    let totalBuy = 0,
      totalSell = 0,
      totalCumBuyQuote = 0,
      totalCumSellQuote = 0,
      avgBuy = 0,
      avgSell = 0,
      profit = 0;

    spotOrders.forEach((order) => {
      if (order.status === 'FILLED' && order.side === 'BUY') {
        totalBuy += parseFloat(order.executedQty);
        totalCumBuyQuote += parseFloat(order.cummulativeQuoteQty);
      } else if (order.status === 'FILLED' && order.side === 'SELL') {
        totalSell += parseFloat(order.executedQty);
        totalCumSellQuote += parseFloat(order.cummulativeQuoteQty);
      }
    });
    setTotal({
      buy: totalBuy,
      sell: totalSell,
      cumBuy: totalCumBuyQuote,
      cumSell: totalCumSellQuote,
    });
    console.log({ totalBuy, totalSell, totalCumBuyQuote, totalCumSellQuote });
    setSpotOrders(spotOrders);

    setLoading(false);
  }, []);

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
                  <TableCell>Buy</TableCell>
                  <TableCell>Sell</TableCell>
                  <TableCell>Cum Buy Quote</TableCell>
                  <TableCell>Cum Sell Quote</TableCell>
                  <TableCell>Profit</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                <TableRow>
                  <TableCell>{total.buy}</TableCell>
                  <TableCell>{total.sell}</TableCell>
                  <TableCell>{total.cumBuy}</TableCell>
                  <TableCell>{total.cumSell}</TableCell>
                  <TableCell>{round4Dec(total.cumSell - total.cumBuy)}</TableCell>
                </TableRow>
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
                  .filter((order) => order.status === 'FILLED')
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
