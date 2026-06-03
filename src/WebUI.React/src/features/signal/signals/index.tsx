import { Chip, Grid, Paper, Tooltip, Typography } from '@mui/material';
import { BackdropLoading } from 'components/backdrop-loading';
import { setTitle } from 'features/layout/slice';
import { useEffect, useMemo, useState } from 'react';
import { useDispatch } from 'react-redux';
import { useGetSignals } from './hooks/use-get-signals';
import { SignalDto } from './types';
import SignalChartDialog from './components/signal-chart-dialog';
import SignalSearchBar from './components/signal-search-bar';
import { Column } from 'components/table/types';
import TableData from 'components/table';

const INTERVAL_TO_BINANCE: Record<string, string> = {
  '1min': '1m',
  '5min': '5m',
  '15min': '15m',
  '30min': '30m',
  '1hour': '1h',
  '4hour': '4h',
  '1day': '1d',
};

const formatDuration = (ms: number): string => {
  const hours = ms / 3_600_000;
  if (hours < 1) return `${Math.round(ms / 60_000)}m`;
  return `${hours.toFixed(1)}h`;
};

const DATE_FORMAT_OPTIONS: Intl.DateTimeFormatOptions = {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
};

const DEFAULT_PAGE_SIZE = 20;

const COLUMNS: Column[] = [
  { id: 'detectedAt', label: 'Detected At' },
  { id: 'type', label: 'Type', align: 'center' },
  { id: 'interval', label: 'Interval', align: 'center' },
  { id: 'entryPrice', label: 'Entry Price', align: 'right' },
  { id: 'stopLoss', label: 'Stop Loss', align: 'right' },
  { id: 'maxProfit', label: 'Max Profit %', align: 'right' },
  { id: 'maxProfitHitAfterMinutes', label: 'Max Profit Hit', align: 'right', sortable: true },
  { id: 'entryHitAfterMinutes', label: 'Entry Hit', align: 'center', sortable: true },
  { id: 'createdAt', label: 'Created At', sortable: true },
  { id: 'stopLossHitAfterMinutes', label: 'SL Hit', align: 'center', sortable: true },
];

export default function Signals() {
  const dispatch = useDispatch();
  const [selectedSignal, setSelectedSignal] = useState<SignalDto | null>(null);
  const [modalInterval, setModalInterval] = useState('');
  const [signalQuery, setSignalQuery] = useState(() => ({
    from: new Date(new Date().getFullYear(), new Date().getMonth() - 1, 1).toISOString(),
    to: new Date().toISOString(),
    interval: '',
    signalType: '',
    sortBy: 'createdAt',
    sortOrder: 'desc' as 'asc' | 'desc',
    pageNumber: 1,
    pageSize: DEFAULT_PAGE_SIZE,
  }));

  useEffect(() => {
    dispatch(setTitle('Signals'));
  }, [dispatch]);

  const { data, isLoading } = useGetSignals(signalQuery);

  const openSignalModal = (row: SignalDto) => {
    setSelectedSignal(row);
    setModalInterval(INTERVAL_TO_BINANCE[row.interval] ?? row.interval);
  };

  const handlePageChange = (newPage: number, newSize: number) => {
    setSignalQuery((prev) => ({ ...prev, pageNumber: newPage + 1, pageSize: newSize }));
  };

  const handleSearch = (params: {
    from: string;
    to: string;
    interval: string;
    signalType: string;
  }) => {
    setSignalQuery({
      from: params.from,
      to: params.to,
      interval: params.interval,
      signalType: params.signalType,
      sortBy: 'createdAt',
      sortOrder: 'desc',
      pageNumber: 1,
      pageSize: DEFAULT_PAGE_SIZE,
    });
  };

  const handleSortChange = (sortBy: string, sortOrder: 'asc' | 'desc') => {
    setSignalQuery((prev) => ({
      ...prev,
      sortBy: sortBy || 'createdAt',
      sortOrder: sortBy ? sortOrder : 'desc',
      pageNumber: 1,
    }));
  };

  const rows = useMemo(
    () =>
      data?.items.map((item) => {
        const type = item.signalType.toUpperCase();
        const detectedAtDate = new Date(item.detectedAt);
        const maxProfitHitDate = item.maxProfitHitAt ? new Date(item.maxProfitHitAt) : null;
        const entryHitDate = item.entryHitAt ? new Date(item.entryHitAt) : null;
        const stopLossHitDate = item.stopLossHitAt ? new Date(item.stopLossHitAt) : null;
        const row: Record<string, unknown> = {
          detectedAt: (
            <Typography
              variant="body2"
              onClick={() => openSignalModal(item)}
              sx={{ cursor: 'pointer', textDecoration: 'underline', display: 'inline' }}
            >
              {detectedAtDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}
            </Typography>
          ),
          type: (
            <Chip
              label={`${type} ${item.leverage}x`}
              color={type === 'LONG' ? 'success' : 'error'}
              size="small"
            />
          ),
          interval: item.interval,
          entryPrice: item.entryPrice.toFixed(2),
          stopLoss: item.stopLoss.toFixed(2),
          maxProfit:
            item.maxProfit > 0 ? (
              <Typography variant="body2" color="success.main">
                +{item.maxProfit.toFixed(2)}%
              </Typography>
            ) : (
              '—'
            ),
          maxProfitHitAfterMinutes:
            item.maxProfitHitAfterMinutes >= 0 && maxProfitHitDate ? (
              <Tooltip title={maxProfitHitDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}>
                <Typography variant="body2" color="success.main" sx={{ cursor: 'default' }}>
                  {formatDuration(item.maxProfitHitAfterMinutes * 60_000)}
                </Typography>
              </Tooltip>
            ) : (
              '—'
            ),
          entryHitAfterMinutes: item.entryHitAfterMinutes >= 0 && entryHitDate ? (
            <Tooltip title={entryHitDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}>
              <Typography variant="body2" color="success.main" sx={{ cursor: 'default' }}>
                {formatDuration(item.entryHitAfterMinutes * 60_000)}
              </Typography>
            </Tooltip>
          ) : (
            '—'
          ),
          createdAt: (
            <Tooltip title={new Date(item.createdAt).toLocaleString('en-US', DATE_FORMAT_OPTIONS)}>
              <Typography variant="body2" sx={{ cursor: 'default', display: 'inline' }}>
                {formatDuration(item.createdAt - item.detectedAt)}
              </Typography>
            </Tooltip>
          ),
          stopLossHitAfterMinutes: item.stopLossHitAfterMinutes >= 0 && stopLossHitDate ? (
            <Tooltip title={stopLossHitDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}>
              <Typography variant="body2" color="error.main" sx={{ cursor: 'default' }}>
                {formatDuration(item.stopLossHitAfterMinutes * 60_000)}
              </Typography>
            </Tooltip>
          ) : (
            '—'
          ),
        };
        return row;
      }) ?? [],
    [data],
  );

  return (
    <Grid id="signals" container sx={{ height: '100%' }}>
      <Grid item xs={12} sx={{ height: '100%' }}>
        <Paper
          sx={{
            p: 2,
            display: 'flex',
            flexDirection: 'column',
            position: 'relative',
            height: '100%',
          }}
        >
          <SignalSearchBar onSearch={handleSearch} />

          <TableData
            isLoading={isLoading}
            columns={COLUMNS}
            count={data?.totalCount ?? 0}
            page={signalQuery.pageNumber - 1}
            rowsPerPage={signalQuery.pageSize}
            onPageChange={handlePageChange}
            data={rows}
            sortBy={signalQuery.sortBy}
            sortOrder={signalQuery.sortOrder}
            onSortChange={handleSortChange}
          />

          <BackdropLoading loading={isLoading} />
        </Paper>
      </Grid>

      {selectedSignal && (
        <SignalChartDialog
          signal={selectedSignal}
          interval={modalInterval}
          onClose={() => setSelectedSignal(null)}
          onIntervalChange={setModalInterval}
        />
      )}
    </Grid>
  );
}
