// redux store and redux are kind of used interchangeably, both stand for a container for JavaScript apps, and it stores the whole state of the app in an immutable object tree. The intended pattern for redux is just to have a single store for your application.

import { configureStore } from '@reduxjs/toolkit';
import counterReducer from 'features/counter/slice';

export const store = configureStore({
  reducer: {
    counter: counterReducer,
  },
});

export type RootState = ReturnType<typeof store.getState>;
