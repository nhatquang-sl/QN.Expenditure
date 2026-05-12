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

export default function Signals() {
  const dispatch = useDispatch();
  const [from, setFrom] = useState<Dayjs>(dayjs().subtract(30, 'day').startOf('day'));
  const [to, setTo] = useState<Dayjs>(dayjs().endOf('day'));
  const [interval, setInterval] = useState('');
  const [signalType, setSignalType] = useState('');
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(20);
  const [committed, setCommitted] = useState({
    from: dayjs().subtract(30, 'day').startOf('day').toISOString(),
    to: dayjs().endOf('day').toISOString(),
    interval: '',
    signalType: '',
    pageNumber: 1,
    pageSize: 20,
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
    <Grid container spacing={3}>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column', position: 'relative' }}>
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
                onChange={(e) => setInterval(e.target.value)}
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

          <TableContainer>
            <Table stickyHeader size="small" aria-label="signals table">
              <TableHead>
                <TableRow>
                  <TableCell>Detected At</TableCell>
                  <TableCell align="center">Type</TableCell>
                  <TableCell align="center">Interval</TableCell>
                  <TableCell align="right">Entry Price</TableCell>
                  <TableCell align="right">Stop Loss</TableCell>
                  <TableCell align="right">Take Profit</TableCell>
                  <TableCell align="center">Leverage</TableCell>
                  <TableCell align="right">Max Profit %</TableCell>
                  <TableCell align="center">Entry Hit</TableCell>
                  <TableCell align="center">SL Hit</TableCell>
                  <TableCell align="center">TP Hit</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data?.items.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={11} align="center">
                      <Typography variant="body2" color="textSecondary" sx={{ py: 4 }}>
                        No signals found for the selected filters.
                      </Typography>
                    </TableCell>
                  </TableRow>
                )}
                {data?.items.map((row) => (
                  <TableRow hover key={row.id}>
                    <TableCell sx={{ whiteSpace: 'nowrap' }}>
                      {new Date(row.detectedAt).toLocaleString('en-US', DATE_FORMAT_OPTIONS)}
                    </TableCell>
                    <TableCell align="center">
                      <Chip
                        label={row.signalType.toUpperCase()}
                        color={row.signalType.toUpperCase() === 'LONG' ? 'success' : 'error'}
                        size="small"
                      />
                    </TableCell>
                    <TableCell align="center">{row.interval}</TableCell>
                    <TableCell align="right">{row.entryPrice.toFixed(2)}</TableCell>
                    <TableCell align="right">{row.stopLoss.toFixed(2)}</TableCell>
                    <TableCell align="right">{row.takeProfit.toFixed(2)}</TableCell>
                    <TableCell align="center">{row.leverage}x</TableCell>
                    <TableCell align="right">
                      {row.maxProfit > 0 ? (
                        <Typography variant="body2" color="success.main">
                          +{(row.maxProfit * 100).toFixed(2)}%
                        </Typography>
                      ) : (
                        '—'
                      )}
                    </TableCell>
                    <TableCell align="center">
                      {row.entryHitAt ? (
                        <Typography variant="caption" color="success.main">
                          {new Date(row.entryHitAt).toLocaleString('en-US', DATE_FORMAT_OPTIONS)}
                        </Typography>
                      ) : (
                        '—'
                      )}
                    </TableCell>
                    <TableCell align="center">
                      {row.stopLossHitAt ? (
                        <Typography variant="caption" color="error.main">
                          {new Date(row.stopLossHitAt).toLocaleString('en-US', DATE_FORMAT_OPTIONS)}
                        </Typography>
                      ) : (
                        '—'
                      )}
                    </TableCell>
                    <TableCell align="center">
                      {row.takeProfitHitAt ? (
                        <Typography variant="caption" color="primary.main">
                          {new Date(row.takeProfitHitAt).toLocaleString(
                            'en-US',
                            DATE_FORMAT_OPTIONS
                          )}
                        </Typography>
                      ) : (
                        '—'
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>

          <TablePagination
            rowsPerPageOptions={[10, 20, 50, 100]}
            component="div"
            count={data?.totalCount ?? 0}
            rowsPerPage={rowsPerPage}
            page={page}
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
