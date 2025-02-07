// The name slice comes from splitting up redux state objects into multiple slices of state. So slice is a collection of reducer logic and actions for a single feature in the app. E.g a blog might have a slice for posts and another slice for comments, you would handle the logic of each differently, so they each get their own slice.
import { createSlice } from '@reduxjs/toolkit';

const SYMBOLS = ['BTCUSDT', 'ETHUSDT', 'BNBUSDT', 'SOLUSDT', 'DOGEUSDT', 'APTUSDT', 'ICPUSDT'];
const INTERVALS = ['5m', '15m', '1h', '4h', '1d'];
const initialState = {
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

export const { setSymbol, setInterval } = spotGridSlice.actions;
export { INTERVALS, SYMBOLS };
export default spotGridSlice.reducer;
