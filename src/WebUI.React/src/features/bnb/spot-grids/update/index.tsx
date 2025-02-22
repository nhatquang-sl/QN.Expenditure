import { zodResolver } from '@hookform/resolvers/zod';
import { Paper } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { BackdropLoading } from 'components/backdrop-loading';
import Form from 'components/form';
import {
  ActionBlock,
  ActionElement,
  Block,
  ComputeElement,
  NumberElement,
} from 'components/form/types';
import { useSelector } from 'react-redux';
import { useParams } from 'react-router-dom';
import { bnbSpotGridClient, RootState } from 'store';
import { SpotGridDto, UpdateSpotGridCommand } from 'store/api-client';
import { GridOrderData, GridOrderSchema, SpotGridSummary } from '../types';
import { toKuCoinSymbol } from '../utils';

// const GRID_MODES = [
//   new InputOption(SpotGridMode.ARITHMETIC, 'arithmetic'),
//   new InputOption(SpotGridMode.GEOMETRIC, 'geometric'),
// ];
// const SYMBOLS = [new InputOption('BTC-USDT'), new InputOption('CYBERUSDT')];

export default function SpotGridUpdate() {
  const { symbol } = useSelector((state: RootState) => state.spotGrid);
  const { id } = useParams();
  console.log({ id });
  const { isLoading, data } = useQuery({
    queryKey: ['SpotGrids', id],
    queryFn: async () => await bnbSpotGridClient.get(parseInt(id ?? '0')),
  });

  const onSubmit = async (data: GridOrderData) => {
    const command = UpdateSpotGridCommand.fromJS({
      ...data,
      symbol: toKuCoinSymbol(symbol),
      id: id,
    });
    await bnbSpotGridClient.update(parseInt(id ?? '0'), command);
  };
  data?.gridSteps;
  const profitOfGrid = new ComputeElement('Summary Grid');
  profitOfGrid.computedValue = (getValues) => {
    const lowerPrice = Number(getValues('lowerPrice'));
    const upperPrice = Number(getValues('upperPrice'));
    const numberOfGrids = Number(getValues('numberOfGrids'));
    const investment = Number(getValues('investment')) * 0.75;
    console.log({ lowerPrice, upperPrice, numberOfGrids, investment });
    return (
      <SpotGridSummary
        lowerPrice={lowerPrice}
        upperPrice={upperPrice}
        numberOfGrids={numberOfGrids}
        investment={investment}
        spotGrid={data ?? new SpotGridDto()}
      />
    );
  };

  if (isLoading) {
    return <BackdropLoading loading={isLoading} />;
  }

  return (
    <Paper sx={{ p: 2 }}>
      <Form
        onSubmit={onSubmit}
        resolver={zodResolver(GridOrderSchema)}
        blocks={[
          new Block([
            new NumberElement('Lower Price', data?.lowerPrice),
            new NumberElement('Upper Price', data?.upperPrice),
            new NumberElement('Trigger Price', data?.triggerPrice, false),
          ]),
          new Block([
            new NumberElement('Number of Grids', data?.numberOfGrids),
            new NumberElement('Investment', data?.investment),
            // new SelectElement('Grid Mode', GRID_MODES[0].value, GRID_MODES, 'none'),
          ]),
          new Block([
            new NumberElement('Take Profit', data?.takeProfit),
            new NumberElement('Stop Loss', data?.stopLoss),
          ]),
          new Block([profitOfGrid]),

          new ActionBlock([new ActionElement('Update', 'submit')]),
        ]}
      />
    </Paper>
  );
}
