import {
  Chip,
  Grid,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material';
import { BackdropLoading } from 'components/backdrop-loading';
import { setTitle } from 'features/layout/slice';
import { useEffect, useState } from 'react';
import { useDispatch } from 'react-redux';
import { useGetSignals } from './hooks/use-get-signals';
import { SignalDto } from './types';
import SignalChartDialog from './components/signal-chart-dialog';
import SignalSearchBar from './components/signal-search-bar';

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

export default function Signals() {
  const dispatch = useDispatch();
  const [selectedSignal, setSelectedSignal] = useState<SignalDto | null>(null);
  const [modalInterval, setModalInterval] = useState('');
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(DEFAULT_PAGE_SIZE);
  const [committed, setCommitted] = useState({
    from: new Date(new Date().getFullYear(), new Date().getMonth() - 1, 1).toISOString(),
    to: new Date().toISOString(),
    interval: '',
    signalType: '',
    pageNumber: 1,
    pageSize: DEFAULT_PAGE_SIZE,
  });

  useEffect(() => {
    dispatch(setTitle('Signals'));
  }, [dispatch]);

  const { data, isLoading } = useGetSignals(committed);

  const openSignalModal = (row: SignalDto) => {
    setSelectedSignal(row);
    setModalInterval(INTERVAL_TO_BINANCE[row.interval] ?? row.interval);
  };

  const handleSearch = (params: { from: string; to: string; interval: string; signalType: string }) => {
    setPage(0);
    setCommitted({
      from: params.from,
      to: params.to,
      interval: params.interval,
      signalType: params.signalType,
      pageNumber: 1,
      pageSize: rowsPerPage,
    });
  };

  return (
    <Grid id="signals" container sx={{ height: '100%' }}>
      <Grid item xs={12} sx={{ height: '100%' }}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column', position: 'relative', height: '100%' }}>
          <SignalSearchBar onSearch={handleSearch} />

          <TableContainer sx={{ flex: 1, minHeight: 0, overflow: 'auto' }}>
            <Table stickyHeader size="small" aria-label="signals table">
              <TableHead>
                <TableRow>
                  <TableCell>Detected At</TableCell>
                  <TableCell align="center">Type</TableCell>
                  <TableCell align="center">Interval</TableCell>
                  <TableCell align="right">Entry Price</TableCell>
                  <TableCell align="right">Stop Loss</TableCell>
                  <TableCell align="right">Max Profit %</TableCell>
                  <TableCell align="right">Max Profit Hit</TableCell>
                  <TableCell align="center">Entry Hit</TableCell>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>Created At</TableCell>
                  <TableCell align="center">SL Hit</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {isLoading && !data && (
                  <TableRow>
                    <TableCell colSpan={10} align="center">
                      <Typography variant="body2" color="textSecondary" sx={{ py: 4 }}>
                        Loading...
                      </Typography>
                    </TableCell>
                  </TableRow>
                )}
                {data?.items.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={10} align="center">
                      <Typography variant="body2" color="textSecondary" sx={{ py: 4 }}>
                        No signals found for the selected filters.
                      </Typography>
                    </TableCell>
                  </TableRow>
                )}
                {data?.items.map((row) => {
                  const type = row.signalType.toUpperCase();
                  const detectedAtDate = new Date(row.detectedAt);
                  const maxProfitHitDate = row.maxProfitHitAt ? new Date(row.maxProfitHitAt) : null;
                  const entryHitDate = row.entryHitAt ? new Date(row.entryHitAt) : null;
                  const stopLossHitDate = row.stopLossHitAt ? new Date(row.stopLossHitAt) : null;
                  return (
                    <TableRow hover key={row.id}>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>
                        <Typography
                          variant="body2"
                          onClick={() => openSignalModal(row)}
                          sx={{ cursor: 'pointer', textDecoration: 'underline', display: 'inline' }}
                        >
                          {detectedAtDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}
                        </Typography>
                      </TableCell>
                      <TableCell align="center">
                        <Chip
                          label={`${type} ${row.leverage}x`}
                          color={type === 'LONG' ? 'success' : 'error'}
                          size="small"
                        />
                      </TableCell>
                      <TableCell align="center">{row.interval}</TableCell>
                      <TableCell align="right">{row.entryPrice.toFixed(2)}</TableCell>
                      <TableCell align="right">{row.stopLoss.toFixed(2)}</TableCell>
                      <TableCell align="right">
                        {row.maxProfit > 0 ? (
                          <Typography variant="body2" color="success.main">
                            +{row.maxProfit.toFixed(2)}%
                          </Typography>
                        ) : (
                          '—'
                        )}
                      </TableCell>
                      <TableCell align="right">
                        {maxProfitHitDate && entryHitDate ? (
                          <Tooltip title={maxProfitHitDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}>
                            <Typography variant="body2" color="success.main" sx={{ cursor: 'default' }}>
                              {formatDuration(maxProfitHitDate.getTime() - entryHitDate.getTime())}
                            </Typography>
                          </Tooltip>
                        ) : (
                          '—'
                        )}
                      </TableCell>
                      <TableCell align="center">
                        {entryHitDate ? (
                          <Tooltip title={entryHitDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}>
                            <Typography variant="body2" color="success.main" sx={{ cursor: 'default' }}>
                              {formatDuration(entryHitDate.getTime() - detectedAtDate.getTime())}
                            </Typography>
                          </Tooltip>
                        ) : (
                          '—'
                        )}
                      </TableCell>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>
                        <Tooltip title={new Date(row.createdAt).toLocaleString('en-US', DATE_FORMAT_OPTIONS)}>
                          <Typography variant="body2" sx={{ cursor: 'default', display: 'inline' }}>
                            {formatDuration(row.createdAt - row.detectedAt)}
                          </Typography>
                        </Tooltip>
                      </TableCell>
                      <TableCell align="center">
                        {stopLossHitDate ? (
                          <Tooltip title={stopLossHitDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}>
                            <Typography variant="body2" color="error.main" sx={{ cursor: 'default' }}>
                              {formatDuration(stopLossHitDate.getTime() - detectedAtDate.getTime())}
                            </Typography>
                          </Tooltip>
                        ) : (
                          '—'
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>

          <TablePagination
            rowsPerPageOptions={[10, 20, 50, 100]}
            component="div"
            count={data?.totalCount ?? 0}
            rowsPerPage={rowsPerPage}
            page={Math.min(page, Math.max(0, Math.ceil((data?.totalCount ?? 0) / rowsPerPage) - 1))}
            onPageChange={(_event, newPage) => {
              setPage(newPage);
              setCommitted((prev) => ({ ...prev, pageNumber: newPage + 1 }));
            }}
            onRowsPerPageChange={(event) => {
              const size = parseInt(event.target.value, 10);
              setRowsPerPage(size);
              setPage(0);
              setCommitted((prev) => ({ ...prev, pageSize: size, pageNumber: 1 }));
            }}
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
