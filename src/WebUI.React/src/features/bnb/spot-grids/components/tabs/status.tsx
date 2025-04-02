import { Grid, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import { SpotGridDto } from 'store/api-client';
import TotalProfit from '../total-profit';

export default function TabStatus(props: { spotGrid: SpotGridDto }) {
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
