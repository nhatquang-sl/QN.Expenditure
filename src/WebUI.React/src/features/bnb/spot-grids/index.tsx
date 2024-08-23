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
import { CreateSpotGridCommand, SpotGridMode } from 'store/api-client';
import Chart from './chart';
import Header from './header';
import { GridOrderData, GridOrderSchema } from './types';

const GRID_MODES = [
  new InputOption(SpotGridMode.ARITHMETIC, 'arithmetic'),
  new InputOption(SpotGridMode.GEOMETRIC, 'geometric'),
];
const SYMBOLS = [new InputOption('BTCUSDT')];
export default function BnbSpotGrids() {
  const onSubmit = async (data: GridOrderData) => {
    const res = await bnbSpotGridClient.create(new CreateSpotGridCommand(data));
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
                new NumberElement('Trigger Price'),
              ]),
              new Block([new NumberElement('Lower Price'), new NumberElement('Upper Price')]),
              new Block([
                new NumberElement('Number of Grids'),
                new SelectElement('Grid Mode', GRID_MODES[0].value, GRID_MODES, 'none'),
              ]),
              new Block([new NumberElement('Investment')]),
              new Block([new NumberElement('Take Profit'), new NumberElement('Stop Loss')]),
              new ActionBlock([new ActionElement('Add', 'submit')]),
            ]}
          />
        </Paper>
      </Grid>
    </Grid>
  );
}
