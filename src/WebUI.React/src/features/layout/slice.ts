// The name slice comes from splitting up redux state objects into multiple slices of state. So slice is a collection of reducer logic and actions for a single feature in the app. E.g a blog might have a slice for posts and another slice for comments, you would handle the logic of each differently, so they each get their own slice.
import { createSlice } from '@reduxjs/toolkit';

export const drawerWidth = 240;
const initialState = {
  open: true,
};

export const layoutSlice = createSlice({
  name: 'layout',
  initialState,
  reducers: {
    toggleDrawer: (state) => {
      state.open = !state.open;
    },
  },
});

export const { toggleDrawer } = layoutSlice.actions;
export default layoutSlice.reducer;
