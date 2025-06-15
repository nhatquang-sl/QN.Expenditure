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
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { candlesClient } from 'store';
import { IntervalType, Kline } from 'store/api-client';
import { round2Dec } from 'store/constants';

type CellData = {
  openTime: Date;
  percentage: number;
};
const today = new Date();
export default function CandleAnalytics() {
  const [month, setMonth] = useState(today.getMonth() - 1);
  const [year, setYear] = useState(today.getFullYear());
  console.log({ month, year });
  const { data, isPending } = useQuery({
    queryKey: ['2fa'],
    queryFn: () => candlesClient.get('BTCUSDT', IntervalType.OneDay),
  });

  const rows: CellData[][] = [[]];
  data
    ?.filter((candle: Kline) => {
      const openTime = candle.openTime.getTime();
      const pre = new Date(year, month - 1, 1).getTime();
      const next = new Date(year, month + 2, 1).getTime();

      return pre <= openTime && openTime < next;
    })
    ?.forEach((candle: Kline, index: number) => {
      const openTime = new Date(candle.openTime);
      console.log(index, openTime.getDay(), openTime.getMonth(), openTime);
      if (index === 0) {
        for (let i = 0; i < openTime.getDay() - 1; i++) {
          rows[rows.length - 1].push({
            openTime: new Date(openTime.getFullYear(), openTime.getMonth(), openTime.getDate() - i),
          } as CellData); // Fill empty cells for days before the first candle
        }
      }
      if (rows[rows.length - 1].length === 7) {
        rows.push([]);
      } else {
        // row.push(candle.closePrice-candle.openPrice > 0 ? '🟢' : '🔴');
        const diff = candle.closePrice - candle.openPrice;
        const percentage = round2Dec((diff / candle.openPrice) * 100);
        rows[rows.length - 1].push({
          openTime: openTime,
          percentage: percentage,
        } as CellData);
      }
    });
  console.log('Candle Analytics Data:', data, isPending);
  return (
    <Grid container spacing={3}>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column' }}>
          <Typography component="h2" variant="h6" color="primary" gutterBottom>
            Candle Analytics
          </Typography>
        </Paper>
      </Grid>
      <Grid item xs={12}>
        <TableContainer component={Paper}>
          <Table sx={{ minWidth: 650 }} aria-label="simple table">
            <TableHead>
              <TableRow>
                <TableCell>Monday</TableCell>
                <TableCell>Tuesday</TableCell>
                <TableCell>Wednesday</TableCell>
                <TableCell>Thursday</TableCell>
                <TableCell>Friday</TableCell>
                <TableCell>Saturday</TableCell>
                <TableCell>Sunday</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((row, index) => (
                <TableRow key={index}>
                  {row.map((cell, cellIndex) => (
                    <TableCell
                      key={cellIndex}
                      sx={{
                        textAlign: 'center',
                        backgroundColor:
                          cell.percentage > 0
                            ? '#c8e6c9' // light green
                            : cell.percentage < 0
                            ? '#ffcdd2' // light red
                            : '#fff', // white
                      }}
                    >
                      {cell.openTime.getDate()}/{cell.openTime.getMonth() + 1}
                      <br />
                      {cell.percentage}%
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Grid>
    </Grid>
  );
}
