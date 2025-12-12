// The name slice comes from splitting up redux state objects into multiple slices of state. So slice is a collection of reducer logic and actions for a single feature in the app. E.g a blog might have a slice for posts and another slice for comments, you would handle the logic of each differently, so they each get their own slice.
import { createSlice, PayloadAction } from '@reduxjs/toolkit';
import { RootState } from 'store';
import { GridDetails } from './types';

const SYMBOLS = ['BTCUSDT', 'ETHUSDT', 'BNBUSDT', 'SOLUSDT', 'DOGEUSDT', 'APTUSDT', 'ICPUSDT'];
const INTERVALS = ['5m', '15m', '1h', '4h', '1d'];

type GridDetailsState = { gridDetails: GridDetails[]; triggerPrice: number };

const gridDetailsInitialState: GridDetailsState = {
  gridDetails: [],
  triggerPrice: 0,
};
export const spotGridDetailsSlice = createSlice({
  name: 'spotGridDetails',
  initialState: gridDetailsInitialState,
  reducers: {
    setGridDetails: (state, action: PayloadAction<GridDetails[]>) => {
      state.gridDetails = action.payload;
    },
    setTriggerPrice: (state, action: PayloadAction<number>) => {
      state.triggerPrice = action.payload;
    },
  },
});

type SpotGridState = {
  symbol: string;
  interval: string;
};
const initialState: SpotGridState = {
  symbol: localStorage.chartSymbol ?? SYMBOLS[0],
  interval: localStorage.chartInterval ?? INTERVALS[0],
};

export const spotGridSlice = createSlice({
  name: 'spotGrid',
  initialState,
  reducers: {
    setSymbol: (state, action) => {
      state.symbol = action.payload;
      localStorage.chartSymbol = action.payload;
    },
    setInterval: (state, action) => {
      state.interval = action.payload;
      localStorage.chartInterval = action.payload;
    },
  },
});

interface PriceState {
  prices: Record<string, number>; // ✅ Use object instead of Map
}

const spotPriceState: PriceState = {
  prices: {},
};
const spotPriceSlice = createSlice({
  name: 'spotPrice',
  initialState: spotPriceState,
  reducers: {
    // Set price for a symbol (receives two parameters: symbol, price)
    setPrice: (state, action: PayloadAction<[string, number]>) => {
      const [symbol, price] = action.payload;
      state.prices[symbol] = price; // Update Map directly
    },
  },
});

export const { setSymbol, setInterval } = spotGridSlice.actions;
export const { setGridDetails, setTriggerPrice } = spotGridDetailsSlice.actions;
export const { setPrice } = spotPriceSlice.actions;
// Custom selector to retrieve a price by symbol
export const selectPrice = (symbol: string) => (state: RootState) =>
  state.spotPrice.prices[symbol] ?? 0;
export { INTERVALS, SYMBOLS };

export const { reducer: spotGridDetailsReducer } = spotGridDetailsSlice;
export const { reducer: spotPriceReducer } = spotPriceSlice;
export default spotGridSlice.reducer;
