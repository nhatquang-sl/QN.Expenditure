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
import { useDispatch, useSelector } from 'react-redux';
import { RootState, spotGridClient } from 'store';
import { CreateSpotGridCommand } from 'store/api-client';
import Tabs from '../components/tabs';
import { setTriggerPrice } from '../slice';
import { GridOrderData, GridOrderSchema } from '../types';

// const GRID_MODES = [
//   new InputOption(SpotGridMode.ARITHMETIC, 'arithmetic'),
//   new InputOption(SpotGridMode.GEOMETRIC, 'geometric'),
// ]

export default function SpotGridCreate() {
  const { symbol } = useSelector((state: RootState) => state.spotGrid);
  const dispatch = useDispatch();

  const onSubmit = async (data: GridOrderData) => {
    const command = { ...data, symbol: symbol } as CreateSpotGridCommand;
    await spotGridClient.create(command);
  };

  const profitOfGrid = new ComputeElement('Summary Grid');
  profitOfGrid.computedValue = (getValues) => {
    const lowerPrice = Number(getValues('lowerPrice'));
    const upperPrice = Number(getValues('upperPrice'));
    const numberOfGrids = Number(getValues('numberOfGrids'));
    const investment = Number(getValues('investment')) * 0.75;

    return (
      <Tabs
        lowerPrice={lowerPrice}
        upperPrice={upperPrice}
        numberOfGrids={numberOfGrids}
        investment={investment}
      />
    );
  };
  const triggerPriceEl = new NumberElement('Trigger Price', 90000);
  triggerPriceEl.watch = (value) => {
    dispatch(setTriggerPrice(Number(value)));
  };

  return (
    <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column', position: 'relative' }}>
      <Form
        onSubmit={onSubmit}
        resolver={zodResolver(GridOrderSchema)}
        blocks={[
          new Block([
            new NumberElement('Lower Price', 80000),
            new NumberElement('Upper Price', 100000),
            triggerPriceEl,
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
