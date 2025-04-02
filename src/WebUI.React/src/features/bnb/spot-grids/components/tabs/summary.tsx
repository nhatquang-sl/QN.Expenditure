import { Grid, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import { useDispatch } from 'react-redux';
import { setGridDetails } from '../../slice';
import { GridDetails } from '../../types';
import { fixedNumber, fixedPercentNumber } from '../../utils';

export default function TabSummary(props: {
  lowerPrice: number;
  upperPrice: number;
  numberOfGrids: number;
  investment: number;
}) {
  const dispatch = useDispatch();
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
    // console.log(grid);
    gridDetails.push(grid);
  }
  dispatch(setGridDetails(gridDetails));

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
