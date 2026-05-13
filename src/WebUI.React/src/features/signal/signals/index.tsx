import {
  Box,
  Button,
  Chip,
  FormControl,
  Grid,
  InputLabel,
  MenuItem,
  Paper,
  Select,
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
import { DatePicker } from '@mui/x-date-pickers';
import { BackdropLoading } from 'components/backdrop-loading';
import dayjs, { Dayjs } from 'dayjs';
import { setTitle } from 'features/layout/slice';
import { useEffect, useState } from 'react';
import { useDispatch } from 'react-redux';
import { useGetSignals } from './hooks/use-get-signals';
import { INTERVALS, SIGNAL_TYPES } from './types';

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
const DEFAULT_FROM = () => dayjs().subtract(1, 'month').startOf('month');
const DEFAULT_TO = () => dayjs().endOf('day');

export default function Signals() {
  const dispatch = useDispatch();
  const [from, setFrom] = useState<Dayjs>(DEFAULT_FROM);
  const [to, setTo] = useState<Dayjs>(DEFAULT_TO);
  const [interval, setIntervalFilter] = useState('');
  const [signalType, setSignalType] = useState('');
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(DEFAULT_PAGE_SIZE);
  const [committed, setCommitted] = useState({
    from: DEFAULT_FROM().toISOString(),
    to: DEFAULT_TO().toISOString(),
    interval: '',
    signalType: '',
    pageNumber: 1,
    pageSize: DEFAULT_PAGE_SIZE,
  });

  useEffect(() => {
    dispatch(setTitle('Signals'));
  }, [dispatch]);

  const { data, isLoading } = useGetSignals(committed);

  const handleSearch = () => {
    setPage(0);
    setCommitted({
      from: from.toISOString(),
      to: to.toISOString(),
      interval,
      signalType,
      pageNumber: 1,
      pageSize: rowsPerPage,
    });
  };

  return (
    <Grid id="signals" container sx={{ height: '100%' }}>
      <Grid item xs={12} sx={{ height: '100%' }}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column', position: 'relative', height: '100%' }}>
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2, mb: 3, alignItems: 'center' }}>
            <DatePicker
              label="From"
              value={from}
              onChange={(v) => v && setFrom(v)}
              slotProps={{ textField: { size: 'small' } }}
            />
            <DatePicker
              label="To"
              value={to}
              onChange={(v) => v && setTo(v)}
              slotProps={{ textField: { size: 'small' } }}
            />
            <FormControl size="small" sx={{ minWidth: 130 }}>
              <InputLabel>Interval</InputLabel>
              <Select
                value={interval}
                label="Interval"
                onChange={(e) => setIntervalFilter(e.target.value)}
              >
                <MenuItem value="">
                  <em>All</em>
                </MenuItem>
                {INTERVALS.map((iv) => (
                  <MenuItem key={iv} value={iv}>
                    {iv}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl size="small" sx={{ minWidth: 130 }}>
              <InputLabel>Signal Type</InputLabel>
              <Select
                value={signalType}
                label="Signal Type"
                onChange={(e) => setSignalType(e.target.value)}
              >
                <MenuItem value="">
                  <em>All</em>
                </MenuItem>
                {SIGNAL_TYPES.map((st) => (
                  <MenuItem key={st} value={st}>
                    {st}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <Button variant="contained" onClick={handleSearch}>
              Search
            </Button>
          </Box>

          {data && (
            <Typography variant="body2" color="textSecondary" sx={{ mb: 1 }}>
              Total: {data.totalCount} records
            </Typography>
          )}

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
                  <TableCell align="center">SL Hit</TableCell>
                  <TableCell align="center">TP Hit</TableCell>
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
                  const maxProfitHitDate = row.maxProfitHitAt ? new Date(row.maxProfitHitAt) : null;
                  const entryHitDate = row.entryHitAt ? new Date(row.entryHitAt) : null;
                  const stopLossHitDate = row.stopLossHitAt ? new Date(row.stopLossHitAt) : null;
                  const takeProfitHitDate = row.takeProfitHitAt ? new Date(row.takeProfitHitAt) : null;

                  return (
                    <TableRow hover key={row.id}>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>
                        {new Date(row.detectedAt).toLocaleString('en-US', DATE_FORMAT_OPTIONS)}
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
                              {((maxProfitHitDate.getTime() - entryHitDate.getTime()) / 3600000).toFixed(1)}h
                            </Typography>
                          </Tooltip>
                        ) : (
                          '—'
                        )}
                      </TableCell>
                      <TableCell align="center">
                        {entryHitDate ? (
                          <Typography variant="caption" color="success.main">
                            {entryHitDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}
                          </Typography>
                        ) : (
                          '—'
                        )}
                      </TableCell>
                      <TableCell align="center">
                        {stopLossHitDate ? (
                          <Typography variant="caption" color="error.main">
                            {stopLossHitDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}
                          </Typography>
                        ) : (
                          '—'
                        )}
                      </TableCell>
                      <TableCell align="center">
                        {takeProfitHitDate ? (
                          <Typography variant="caption" color="primary.main">
                            {takeProfitHitDate.toLocaleString('en-US', DATE_FORMAT_OPTIONS)}
                          </Typography>
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
    </Grid>
  );
}
