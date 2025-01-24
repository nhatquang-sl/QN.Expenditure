import { zodResolver } from '@hookform/resolvers/zod';
import { Grid, Paper } from '@mui/material';
import Form from 'components/form';
import {
  ActionBlock,
  ActionElement,
  Block,
  InputOption,
  NumberElement,
  SelectElement,
} from 'components/form/types';
import { bnbSpotGridClient } from 'store';
import { CreateSpotGridCommand } from 'store/api-client';
import Chart from '../chart';
import Header from '../header';
import { GridOrderData, GridOrderSchema } from '../types';

// const GRID_MODES = [
//   new InputOption(SpotGridMode.ARITHMETIC, 'arithmetic'),
//   new InputOption(SpotGridMode.GEOMETRIC, 'geometric'),
// ];
const SYMBOLS = [new InputOption('BTCUSDT'), new InputOption('CYBERUSDT')];
export default function BnbCreateSpotGrids() {
  const onSubmit = async (data: GridOrderData) => {
    const range = data.upperPrice - data.lowerPrice;
    const amount = range / data.numberOfGrids;
    for (let i = 0; i < data.numberOfGrids; i++) {
      console.log(
        `[${i + 1}] ${data.lowerPrice + amount * i} - ${data.lowerPrice + amount * (i + 1)}`
      );
      console.log(
        `[${i + 1}] ${data.investment / (data.lowerPrice + amount * i)} - ${
          data.investment / (data.lowerPrice + amount * (i + 1))
        }`
      );
    }
    console.log(data.upperPrice - data.lowerPrice);
    const res = await bnbSpotGridClient.create(data as CreateSpotGridCommand);
    console.log(res);
  };

  return (
    <Grid container>
      <Grid item xs={12}>
        <Chart pair="BTCUSDT"></Chart>
      </Grid>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column', position: 'relative' }}>
          <Header />
          <Form
            onSubmit={onSubmit}
            resolver={zodResolver(GridOrderSchema)}
            blocks={[
              new Block([
                new SelectElement('Symbol', SYMBOLS[0].value, SYMBOLS, 'none'),
                new NumberElement('Trigger Price', 3000),
              ]),
              new Block([
                new NumberElement('Lower Price', 2000),
                new NumberElement('Upper Price', 5000),
              ]),
              new Block([
                new NumberElement('Number of Grids', 10),
                // new SelectElement('Grid Mode', GRID_MODES[0].value, GRID_MODES, 'none'),
              ]),
              new Block([new NumberElement('Investment', 100)]),
              new Block([new NumberElement('Take Profit'), new NumberElement('Stop Loss')]),
              new ActionBlock([new ActionElement('Add', 'submit')]),
            ]}
          />
        </Paper>
      </Grid>
    </Grid>
  );
}
