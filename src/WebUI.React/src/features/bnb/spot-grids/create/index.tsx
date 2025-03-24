import { zodResolver } from '@hookform/resolvers/zod';
import { Paper } from '@mui/material';
import Form from 'components/form';
import {
  ActionBlock,
  ActionElement,
  Block,
  ComputeElement,
  NumberElement,
} from 'components/form/types';
import { useSelector } from 'react-redux';
import { bnbSpotGridClient, RootState } from 'store';
import { CreateSpotGridCommand } from 'store/api-client';
import { GridOrderData, GridOrderSchema, SpotGridSummary } from '../types';

// const GRID_MODES = [
//   new InputOption(SpotGridMode.ARITHMETIC, 'arithmetic'),
//   new InputOption(SpotGridMode.GEOMETRIC, 'geometric'),
// ];
// const SYMBOLS = [new InputOption('BTC-USDT'), new InputOption('CYBERUSDT')];

export default function SpotGridCreate() {
  const { symbol } = useSelector((state: RootState) => state.spotGrid);

  const onSubmit = async (data: GridOrderData) => {
    const command = { ...data, symbol: symbol } as CreateSpotGridCommand;
    await bnbSpotGridClient.create(command);
  };

  const profitOfGrid = new ComputeElement('Summary Grid');
  profitOfGrid.computedValue = (getValues) => {
    const lowerPrice = Number(getValues('lowerPrice'));
    const upperPrice = Number(getValues('upperPrice'));
    const numberOfGrids = Number(getValues('numberOfGrids'));
    const investment = Number(getValues('investment')) * 0.75;
    return (
      <SpotGridSummary
        lowerPrice={lowerPrice}
        upperPrice={upperPrice}
        numberOfGrids={numberOfGrids}
        investment={investment}
      />
    );
  };

  return (
    <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column', position: 'relative' }}>
      <Form
        onSubmit={onSubmit}
        resolver={zodResolver(GridOrderSchema)}
        blocks={[
          new Block([
            new NumberElement('Lower Price', 2000),
            new NumberElement('Upper Price', 5000),
            new NumberElement('Trigger Price', 3000),
          ]),
          new Block([
            new NumberElement('Number of Grids', 10),
            new NumberElement('Investment', 100),
            // new SelectElement('Grid Mode', GRID_MODES[0].value, GRID_MODES, 'none'),
          ]),
          new Block([new NumberElement('Take Profit'), new NumberElement('Stop Loss')]),
          new Block([profitOfGrid]),

          new ActionBlock([new ActionElement('Add', 'submit')]),
        ]}
      />
    </Paper>
  );
}
