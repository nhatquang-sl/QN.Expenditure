import { createBrowserRouter, RouterProvider } from 'react-router-dom';

import { Box, CssBaseline } from '@mui/material';
import { createTheme, ThemeProvider } from '@mui/material/styles';
import { LocalizationProvider } from '@mui/x-date-pickers';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import NotFound from 'components/errors/not-found';

import Login from 'features/auth/login';
import LoginHistory from 'features/auth/login-history';
import RegisterConfirm from 'features/auth/register-confirm';
import RequestActivateEmail from 'features/auth/request-activate-email';
import CandleAnalytics from 'features/bnb/candle-analytics';
import SpotGrid from 'features/bnb/spot-grids';
import SpotGridCreate from 'features/bnb/spot-grids/create';
import SpotGridList from 'features/bnb/spot-grids/list';
import SpotGridUpdate from 'features/bnb/spot-grids/update';
import BnbSpotOrders from 'features/bnb/spot-orders';
import BnbSpotOrdersSyncSettings from 'features/bnb/sync-settings';
import Landing from 'features/landing';
import Header from 'features/layout/header';
import Main from 'features/layout/main';
import Sidebar from 'features/layout/sidebar';
import ExchangeSetting from 'features/settings/exchange-setting';
import ExchangeSettingCreate from 'features/settings/exchange-setting/create';
import ExchangeSettingUpdate from 'features/settings/exchange-setting/update';
import SyncSetting from 'features/settings/sync-setting';
import SyncSettingCreate from 'features/settings/sync-setting/create';
import SyncSettingUpdate from 'features/settings/sync-setting/update';
import TradeHistory from 'features/trade/trade-history';
import './App.css';
const defaultTheme = createTheme();
const router = createBrowserRouter([
  {
    path: '/login',
    element: <Login />,
  },
  {
    path: '/request-activate-email',
    element: <RequestActivateEmail />,
  },
  {
    path: '/register-confirm',
    element: <RegisterConfirm />,
  },
  {
    path: '/',
    element: (
      <ThemeProvider theme={defaultTheme}>
        <Box sx={{ display: 'flex' }}>
          <CssBaseline />
          <Header />
          <Sidebar />
          <Main />
        </Box>
      </ThemeProvider>
    ),
    errorElement: <NotFound />,
    children: [
      { index: true, element: <Landing /> },
      {
        path: 'login-history',
        element: <LoginHistory />,
      },
      {
        path: 'settings/exchange-setting',
        element: <ExchangeSetting />,
        children: [
          { index: true, element: <ExchangeSettingCreate /> },
          { path: ':exchangeName', element: <ExchangeSettingUpdate /> },
        ],
      },
      {
        path: 'sync/sync-setting',
        element: <SyncSetting />,
        children: [
          { index: true, element: <SyncSettingCreate /> },
          { path: ':symbol', element: <SyncSettingUpdate /> },
        ],
      },
      {
        path: 'bnb/sync-settings',
        element: <BnbSpotOrdersSyncSettings />,
      },
      {
        path: 'bnb/spot-orders',
        element: <BnbSpotOrders />,
      },
      {
        path: 'bnb/spot-grids',
        element: <SpotGrid />,
        children: [
          { index: true, element: <SpotGridList /> },
          { path: 'create', element: <SpotGridCreate /> },
          {
            path: ':id',
            element: <SpotGridUpdate />,
          },
        ],
      },
      {
        path: '/candle-analytics',
        element: <CandleAnalytics />,
      },
      {
        path: 'trade/history/:symbol',
        element: <TradeHistory />,
      },
      // {
      //   path: 'bnb/spot-grids/create',
      //   element: <BnbCreateSpotGrids />,
      // },
    ],
  },
]);

function App() {
  return (
    <LocalizationProvider dateAdapter={AdapterDayjs}>
      <RouterProvider router={router} />
    </LocalizationProvider>
  );
}

export default App;
