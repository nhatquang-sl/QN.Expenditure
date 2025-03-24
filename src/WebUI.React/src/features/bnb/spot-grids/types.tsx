import { TabContext, TabList, TabPanel } from '@mui/lab';
import {
  Grid,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { SpotGridDto, SpotGridMode } from 'store/api-client';
import { z } from 'zod';
import TotalProfit from './components/total-profit';
import { fixedNumber, fixedPercentNumber } from './utils';

export const GridOrderSchema = z
  .object({
    // symbol: z.string(),
    triggerPrice: z.number(),
    lowerPrice: z.number(),
    upperPrice: z.number(),
    numberOfGrids: z.number(),
    gridMode: z.number().default(SpotGridMode.ARITHMETIC),
    investment: z.number(),
    takeProfit: z.coerce.number().nullable(),
    stopLoss: z.coerce.number().nullable(),
  })
  .refine((data) => data.upperPrice > data.lowerPrice, {
    message: 'Upper Price must be greater than Lower Price',
    path: ['upperPrice'],
  });

export type GridOrderData = z.infer<typeof GridOrderSchema>;

type GridDetails = {
  buyPrice: number;
  sellPrice: number;
  profit: number;
  profitPercent: number;
};

function SummaryTab(props: {
  lowerPrice: number;
  upperPrice: number;
  numberOfGrids: number;
  investment: number;
}) {
  const { lowerPrice, upperPrice, numberOfGrids, investment } = props;
  if (!lowerPrice || !upperPrice || !numberOfGrids) return <></>;

  // https://www.binance.com/en/support/faq/binance-spot-grid-trading-parameters-688ff6ff08734848915de76a07b953dd
  const differentPrice = (upperPrice - lowerPrice) / numberOfGrids;
  if (differentPrice < 0) return <></>;
  const fee = 0.1 / 100;
  const minPercent = fixedPercentNumber(
      (upperPrice * (1 - fee)) / (upperPrice - differentPrice) - 1 - fee
    ),
    maxPercent = fixedPercentNumber(((1 - fee) * differentPrice) / lowerPrice - 2 * fee);
  const amountPerGrid = investment / numberOfGrids;
  console.log({ amountPerGrid });
  const gridDetails: GridDetails[] = [];
  for (let i = 0; i < numberOfGrids; i++) {
    const buyPrice = fixedNumber(lowerPrice + i * differentPrice);
    const sellPrice = fixedNumber(lowerPrice + (i + 1) * differentPrice);
    const profitPercent = fixedPercentNumber(((1 - fee) * differentPrice) / buyPrice - 2 * fee);
    const grid = {
      buyPrice: buyPrice,
      sellPrice: sellPrice,
      profit: amountPerGrid ? fixedNumber((amountPerGrid * profitPercent) / 100) : 0,
      profitPercent: profitPercent,
    };
    console.log(grid);
    gridDetails.push(grid);
  }

  return (
    <>
      <Grid container spacing={0}>
        <Grid item xs={6} md={2}>
          <Typography variant="body2">Profits/Grid</Typography>
        </Grid>
        <Grid item xs={6} md={10}>
          <Typography variant="subtitle2">
            {minPercent}%-{maxPercent}%
          </Typography>
        </Grid>
        <Grid item xs={6} md={2}>
          <Typography variant="body2">Amount/Grid</Typography>
        </Grid>
        <Grid item xs={6} md={10}>
          <Typography variant="subtitle2">{amountPerGrid}</Typography>
        </Grid>
      </Grid>
      <Table size="small" aria-label="a dense table">
        <TableHead>
          <TableRow>
            <TableCell>Buy</TableCell>
            <TableCell>Sell</TableCell>
            <TableCell align="right">Profits</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {gridDetails.map((row) => (
            <TableRow key={row.buyPrice} sx={{ '&:last-child td, &:last-child th': { border: 0 } }}>
              <TableCell>{row.buyPrice}</TableCell>
              <TableCell>{row.sellPrice}</TableCell>
              <TableCell align="right">
                {row.profitPercent}%{row.profit <= 0 ? '' : `~${row.profit}`}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </>
  );
}

function StatusTab(props: { spotGrid: SpotGridDto }) {
  const { gridSteps, profit } = props.spotGrid;
  return (
    <>
      <Grid container spacing={0}>
        <Grid item xs={6} md={2}>
          <Typography variant="body2">Profit</Typography>
        </Grid>
        <Grid item xs={6} md={10}>
          <Typography>{profit}</Typography>
        </Grid>
        <Grid item xs={6} md={2}>
          <Typography variant="body2">Total Profit</Typography>
        </Grid>
        <Grid item xs={6} md={10}>
          <TotalProfit {...props} />
        </Grid>
      </Grid>
      <Table size="small" aria-label="a dense table">
        <TableHead>
          <TableRow>
            <TableCell>Buy</TableCell>
            <TableCell>Sell</TableCell>
            <TableCell>Qty</TableCell>
            <TableCell align="right">Status</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {gridSteps?.map((row) => (
            <TableRow key={row.buyPrice} sx={{ '&:last-child td, &:last-child th': { border: 0 } }}>
              <TableCell>{row.buyPrice}</TableCell>
              <TableCell>{row.sellPrice}</TableCell>
              <TableCell>{row.qty}</TableCell>
              <TableCell align="right">{row.status}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </>
  );
}

export function SpotGridSummary(props: {
  lowerPrice: number;
  upperPrice: number;
  numberOfGrids: number;
  investment: number;
  spotGrid?: SpotGridDto;
}) {
  const [tabIndex, setTabIndex] = useState('summary');
  // const { lowerPrice, upperPrice, numberOfGrids, investment } = props;

  const handleChange = (_event: React.SyntheticEvent, newValue: string) => {
    setTabIndex(newValue);
  };

  return (
    <Grid container spacing={0}>
      <Grid item xs={12}>
        <TabContext value={tabIndex}>
          <TabList onChange={handleChange} aria-label="lab API tabs example">
            <Tab label="Summary" value="summary" />
            {props.spotGrid && <Tab label="Status" value="status" />}
            {props.spotGrid && <Tab label="History" value="history" />}
          </TabList>
          <TabPanel value="summary">
            <SummaryTab {...props} />
          </TabPanel>
          <TabPanel value="status">
            {props.spotGrid && <StatusTab spotGrid={props.spotGrid} />}
          </TabPanel>
          <TabPanel value="history">Item Three</TabPanel>
        </TabContext>
      </Grid>
    </Grid>
  );
}
