import { configureStore } from '@reduxjs/toolkit';
import axios from 'axios';
import snackbarSlice from 'components/snackbar/slice';
import authReducer from 'features/auth/slice';
import counterReducer from 'features/counter/slice';
import layoutReducer from 'features/layout/slice';
import { AuthClient } from './api-client';
import { API_ENDPOINT } from './constants';

export const store = configureStore({
  reducer: {
    counter: counterReducer,
    auth: authReducer,
    snackbar: snackbarSlice,
    layout: layoutReducer,
  },
});

export type RootState = ReturnType<typeof store.getState>;

// Create instance
let instance = axios.create();

// Set the AUTH token for any request
instance.interceptors.request.use(function (config) {
  console.log(store.getState().auth);
  const token = store.getState().auth.accessToken;
  config.headers.Authorization = token ? `Bearer ${token}` : '';
  return config;
});

const authClient = new AuthClient(API_ENDPOINT, instance);

export { authClient };
