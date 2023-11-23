// The name slice comes from splitting up redux state objects into multiple slices of state. So slice is a collection of reducer logic and actions for a single feature in the app. E.g a blog might have a slice for posts and another slice for comments, you would handle the logic of each differently, so they each get their own slice.
import { createSlice } from '@reduxjs/toolkit';

const initialState = {
  count: 0,
};

export const counterSlice = createSlice({
  name: 'counter',
  initialState,
  reducers: {
    increment: (state) => {
      state.count += 1;
    },
    decrement: (state) => {
      state.count -= 1;
    },
    reset: (state) => {
      state.count = 0;
    },
    incrementByAmount: (state, action) => {
      state.count += action.payload;
    },
  },
});

export const { increment, decrement, reset, incrementByAmount } = counterSlice.actions;
export default counterSlice.reducer;
