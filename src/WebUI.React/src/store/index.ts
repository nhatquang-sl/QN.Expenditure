import { configureStore } from '@reduxjs/toolkit';
import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';
import snackbarReducer from 'components/snackbar/slice';
import authReducer, { logout } from 'features/auth/slice';

import spotGridReducer, {
  spotGridDetailsReducer,
  spotPriceReducer,
} from 'features/bnb/spot-grids/slice';

import counterReducer from 'features/counter/slice';
import layoutReducer from 'features/layout/slice';
import {
  AuthClient,
  BnbSettingClient,
  BnbSpotClient,
  CandlesClient,
  ExchangeSettingsClient,
  SpotGridClient,
  SyncSettingsClient,
  TradeClient,
} from './api-client';
import { API_ENDPOINT } from './constants';

export const store = configureStore({
  reducer: {
    counter: counterReducer,
    auth: authReducer,
    snackbar: snackbarReducer,
    layout: layoutReducer,
    spotGrid: spotGridReducer,
    spotPrice: spotPriceReducer,
    spotGridDetails: spotGridDetailsReducer,
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
const instance = axios.create({
  withCredentials: true,
});

const authClient = new AuthClient(API_ENDPOINT, instance);
const bnbSpotClient = new BnbSpotClient(API_ENDPOINT, instance);
const bnbSettingClient = new BnbSettingClient(API_ENDPOINT, instance);
const spotGridClient = new SpotGridClient(API_ENDPOINT, instance);
const candlesClient = new CandlesClient(API_ENDPOINT, instance);
const exchangeSettingsClient = new ExchangeSettingsClient(API_ENDPOINT, instance);
const syncSettingsClient = new SyncSettingsClient(API_ENDPOINT, instance);
const tradeClient = new TradeClient(API_ENDPOINT, instance);

// --- 401 Interceptor: auto-refresh tokens ---
let isRefreshing = false;
let failedQueue: Array<{ resolve: (v: unknown) => void; reject: (e: unknown) => void }> = [];

const processQueue = (error: unknown) => {
  failedQueue.forEach(({ resolve, reject }) => (error ? reject(error) : resolve(undefined)));
  failedQueue = [];
};

instance.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    if (error.response?.status !== 401 || originalRequest._retry) {
      return Promise.reject(error);
    }

    if (isRefreshing) {
      return new Promise((resolve, reject) => {
        failedQueue.push({ resolve, reject });
      })
        .then(() => instance(originalRequest))
        .catch((err) => Promise.reject(err));
    }

    originalRequest._retry = true;
    isRefreshing = true;

    try {
      await instance.post(`${API_ENDPOINT}/api/auth/refresh`);
      processQueue(null);
      return instance(originalRequest);
    } catch (refreshError) {
      processQueue(refreshError);
      store.dispatch(logout());
      if (window.location.pathname !== '/login') {
        window.location.href = '/login';
      }
      return Promise.reject(refreshError);
    } finally {
      isRefreshing = false;
    }
  },
);

export {
  authClient,
  bnbSettingClient,
  bnbSpotClient,
  candlesClient,
  exchangeSettingsClient,
  instance,
  spotGridClient,
  syncSettingsClient,
  tradeClient,
};
