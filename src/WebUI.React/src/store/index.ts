import { configureStore } from '@reduxjs/toolkit';
import axios from 'axios';
import snackbarReducer from 'components/snackbar/slice';
import authReducer from 'features/auth/slice';

import counterReducer from 'features/counter/slice';
import layoutReducer from 'features/layout/slice';
import { AuthClient, BnbSettingClient, BnbSpotClient, BnbSpotGridClient } from './api-client';
import { API_ENDPOINT } from './constants';

export const store = configureStore({
  reducer: {
    counter: counterReducer,
    auth: authReducer,
    snackbar: snackbarReducer,
    layout: layoutReducer,
  },
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware({
      serializableCheck: {
        // Ignore these field paths in all actions
        ignoredActionPaths: ['payload'],
        // Ignore these paths in the state
        ignoredPaths: ['bnbSpotOrders.syncSettings'],
      },
    }),
});

export type RootState = ReturnType<typeof store.getState>;

// Create instance
let instance = axios.create();

// Set the AUTH token for any request
instance.interceptors.request.use(
  function (config) {
    // console.log(store.getState().auth);
    const token = store.getState().auth.accessToken;
    config.headers.Authorization = token ? `Bearer ${token}` : '';
    return config;
  },
  function (error) {
    console.log({ error });
  }
);

const authClient = new AuthClient(API_ENDPOINT, instance);
const bnbSpotClient = new BnbSpotClient(API_ENDPOINT, instance);
const bnbSettingClient = new BnbSettingClient(API_ENDPOINT, instance);
const bnbSpotGridClient = new BnbSpotGridClient(API_ENDPOINT, instance);

export { authClient, bnbSettingClient, bnbSpotClient, bnbSpotGridClient };
