import { zodResolver } from '@hookform/resolvers/zod';
import { Grid, Paper } from '@mui/material';
import Form from 'components/form';
import {
  ActionBlock,
  ActionElement,
  Block,
  InputElement,
  NumberElement,
} from 'components/form/types';
import { useSelector } from 'react-redux';
import { bnbSpotGridClient, RootState } from 'store';
import { CreateSpotGridCommand } from 'store/api-client';
import { GridOrderData, GridOrderSchema } from '../types';
import { fixedNumber, toKuCoinSymbol } from '../utils';

// const GRID_MODES = [
//   new InputOption(SpotGridMode.ARITHMETIC, 'arithmetic'),
//   new InputOption(SpotGridMode.GEOMETRIC, 'geometric'),
// ];
// const SYMBOLS = [new InputOption('BTC-USDT'), new InputOption('CYBERUSDT')];
type GridDetails = {
  buyPrice: number;
  sellPrice: number;
  profit: number;
  profitPercent: number;
};
export default function SpotGridCreate() {
  const { symbol } = useSelector((state: RootState) => state.spotGrid);

  const onSubmit = async (data: GridOrderData) => {
    const command = { ...data, symbol: toKuCoinSymbol(symbol) } as CreateSpotGridCommand;
    await bnbSpotGridClient.create(command);
  };

  const sizeOfGrid = new InputElement('compute', 'Size', '-');
  const profitOfGrid = new InputElement('compute', 'Profit per Grid', '-');
  profitOfGrid.computedValue = (getValues) => {
    const lowerPrice = Number(getValues('lowerPrice'));
    const upperPrice = Number(getValues('upperPrice'));
    const numberOfGrids = Number(getValues('numberOfGrids'));
    const investment = Number(getValues('investment')) * 0.75;
    if (!lowerPrice || !upperPrice || !numberOfGrids) return profitOfGrid.defaultValue as string;

    // https://www.binance.com/en/support/faq/binance-spot-grid-trading-parameters-688ff6ff08734848915de76a07b953dd
    const differentPrice = (upperPrice - lowerPrice) / numberOfGrids;
    if (differentPrice < 0) return profitOfGrid.defaultValue as string;
    const fee = 0.1 / 100;
    const minPercent = (upperPrice * (1 - fee)) / (upperPrice - differentPrice) - 1 - fee,
      maxPercent = ((1 - fee) * differentPrice) / lowerPrice - 2 * fee;
    const gridDetails: GridDetails[] = [];
    for (let i = 0; i < numberOfGrids; i++) {
      const buyPrice = lowerPrice + i * differentPrice;
      const sellPrice = lowerPrice + (i + 1) * differentPrice;
      const profitPercent = fixedNumber(100 * (((1 - fee) * differentPrice) / buyPrice - 2 * fee));
      const investmentPerGrid = investment / numberOfGrids;
      const grid = {
        buyPrice: buyPrice,
        sellPrice: sellPrice,
        profit: fixedNumber((investmentPerGrid * profitPercent) / 100),
        profitPercent: profitPercent,
      };
      gridDetails.push(grid);
      console.log({
        buyPrice,
        sellPrice,
        profitPercent: profitPercent,
        investmentPerGrid,
        profit: fixedNumber((investmentPerGrid * profitPercent) / 100),
      });
    }

    return `${fixedNumber(100 * minPercent)}% - ${fixedNumber(100 * maxPercent)}%`;
  };

  sizeOfGrid.computedValue = (getValues) => {
    const lowerPrice = Number(getValues('lowerPrice'));
    const upperPrice = Number(getValues('upperPrice'));
    const numberOfGrids = Number(getValues('numberOfGrids'));
    if (!lowerPrice || !upperPrice || !numberOfGrids) return sizeOfGrid.defaultValue as string;

    const stepSize = (upperPrice - lowerPrice) / numberOfGrids;
    if (stepSize < 0) return sizeOfGrid.defaultValue as string;
    return fixedNumber(stepSize).toString();
  };

  const buyPriceEl = new InputElement('compute', 'Buy', '-', 'none');
  buyPriceEl.computedValue = (getValues) => {
    const lowerPrice = Number(getValues('lowerPrice'));
    const upperPrice = Number(getValues('upperPrice'));
    const numberOfGrids = Number(getValues('numberOfGrids'));
    if (!lowerPrice || !upperPrice || !numberOfGrids) return buyPriceEl.defaultValue as string;

    // https://www.binance.com/en/support/faq/binance-spot-grid-trading-parameters-688ff6ff08734848915de76a07b953dd
    const differentPrice = (upperPrice - lowerPrice) / numberOfGrids;
    if (differentPrice < 0) return buyPriceEl.defaultValue as string;

    const buyLine: string[] = [];
    for (let i = 0; i < numberOfGrids; i++) {
      const buyPrice = lowerPrice + i * differentPrice;
      buyLine.push(buyPrice.toString());
    }
    return buyLine.join('<br>');
  };

  const sellPriceEl = new InputElement('compute', 'Sell', '-', 'none');
  sellPriceEl.computedValue = (getValues) => {
    const lowerPrice = Number(getValues('lowerPrice'));
    const upperPrice = Number(getValues('upperPrice'));
    const numberOfGrids = Number(getValues('numberOfGrids'));
    if (!lowerPrice || !upperPrice || !numberOfGrids) return sellPriceEl.defaultValue as string;

    // https://www.binance.com/en/support/faq/binance-spot-grid-trading-parameters-688ff6ff08734848915de76a07b953dd
    const differentPrice = (upperPrice - lowerPrice) / numberOfGrids;
    if (differentPrice < 0) return sellPriceEl.defaultValue as string;

    const sellLine: string[] = [];
    for (let i = 0; i < numberOfGrids; i++) {
      const sellPrice = lowerPrice + (i + 1) * differentPrice;
      sellLine.push(sellPrice.toString());
    }
    return sellLine.join('<br>');
  };

  const profitEl = new InputElement('compute', 'Profits', '-', 'none');
  profitEl.computedValue = (getValues) => {
    const lowerPrice = Number(getValues('lowerPrice'));
    const upperPrice = Number(getValues('upperPrice'));
    const numberOfGrids = Number(getValues('numberOfGrids'));
    const investment = Number(getValues('investment')) * 0.75;
    if (!lowerPrice || !upperPrice || !numberOfGrids) return profitEl.defaultValue as string;

    // https://www.binance.com/en/support/faq/binance-spot-grid-trading-parameters-688ff6ff08734848915de76a07b953dd
    const differentPrice = (upperPrice - lowerPrice) / numberOfGrids;
    if (differentPrice < 0) return profitEl.defaultValue as string;
    const fee = 0.1 / 100;

    const profitLine: string[] = [];
    for (let i = 0; i < numberOfGrids; i++) {
      const buyPrice = lowerPrice + i * differentPrice;
      const profitPercent = fixedNumber(100 * (((1 - fee) * differentPrice) / buyPrice - 2 * fee));
      const investmentPerGrid = investment / numberOfGrids;

      profitLine.push(
        `${fixedNumber((investmentPerGrid * profitPercent) / 100)}(${profitPercent}%)`
      );
    }
    return profitLine.join('<br>');
  };

  return (
    <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column', position: 'relative' }}>
      <Grid container>
        <Grid item xs={12}>
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
              new Block([profitOfGrid, sizeOfGrid]),
              new Block([buyPriceEl, sellPriceEl, profitEl]),

              new ActionBlock([new ActionElement('Add', 'submit')]),
            ]}
          />
        </Grid>
      </Grid>
    </Paper>
  );
}
