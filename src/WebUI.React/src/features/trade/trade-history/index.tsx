import { ArrowBack } from '@mui/icons-material';
import {
  Box,
  Button,
  Chip,
  FormControl,
  Grid,
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
import { BackdropLoading } from 'components/backdrop-loading';
import { setTitle } from 'features/layout/slice';
import { useEffect, useMemo, useState } from 'react';
import { useDispatch } from 'react-redux';
import { useNavigate, useParams } from 'react-router-dom';
import { useGetSyncSettings } from './hooks/use-get-sync-settings';
import { useGetTradeHistories } from './hooks/use-get-trade-histories';
import { useGetTradeStatistics } from './hooks/use-get-trade-statistics';
import { columns } from './types';

const DATE_FORMAT_OPTIONS: Intl.DateTimeFormatOptions = {
  year: 'numeric',
  month: 'long',
  day: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: false,
};

export default function TradeHistory() {
  const { symbol } = useParams<{ symbol: string }>();
  const navigate = useNavigate();
  const dispatch = useDispatch();
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(20);

  const { data: syncSettings } = useGetSyncSettings();
  const { data, isLoading } = useGetTradeHistories(symbol || '', page + 1, rowsPerPage);
  const {
    data: stats,
    isLoading: statsLoading,
    isError: statsError,
  } = useGetTradeStatistics(symbol || '');

  useEffect(() => {
    dispatch(setTitle('Trade History'));
  }, [dispatch]);

  const availableSymbols = useMemo(() => {
    if (!syncSettings) return [];
    return syncSettings.map((s) => s.symbol).sort();
  }, [syncSettings]);

  const formattedStats = useMemo(() => {
    if (!stats) return null;
    return {
      buy: {
        totalFunds: stats.buy.totalFunds.toFixed(2),
        totalFee: stats.buy.totalFee.toFixed(4),
        totalSize: stats.buy.totalSize.toFixed(8),
        avgPrice: stats.buy.avgPrice.toFixed(2),
      },
      sell: {
        totalFunds: stats.sell.totalFunds.toFixed(2),
        totalFee: stats.sell.totalFee.toFixed(4),
        totalSize: stats.sell.totalSize.toFixed(8),
        avgPrice: stats.sell.avgPrice.toFixed(2),
      },
    };
  }, [stats]);

  return (
    <Grid container spacing={3}>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column', position: 'relative' }}>
          <Box
            sx={{ mb: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
          >
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
              <Button
                onClick={() => navigate(-1)}
                size="small"
                sx={{ minWidth: 'auto', p: 1, color: 'action.active' }}
              >
                <ArrowBack />
              </Button>
              <FormControl size="small" sx={{ minWidth: 150 }}>
                <Select
                  value={symbol || ''}
                  onChange={(e) => {
                    navigate(`/trade/history/${e.target.value}`);
                    setPage(0);
                  }}
                  displayEmpty
                  disabled={!availableSymbols.length}
                >
                  {availableSymbols.map((s) => (
                    <MenuItem key={s} value={s}>
                      {s}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Box>
            {data && (
              <Typography variant="body2" color="textSecondary">
                Total: {data.totalCount} trades
              </Typography>
            )}
          </Box>

          <Box sx={{ mb: 2 }}>
            {statsLoading && (
              <Typography variant="body2" color="textSecondary">
                Loading statistics...
              </Typography>
            )}
            {statsError && (
              <Typography variant="body2" color="error">
                Failed to load statistics
              </Typography>
            )}
            {formattedStats && (
              <Grid container spacing={2}>
                <Grid item xs={12} md={6}>
                  <Paper variant="outlined" sx={{ p: 2 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
                      <Chip label="BUY" color="success" size="small" />
                      <Typography variant="subtitle2" color="text.secondary">
                        Statistics
                      </Typography>
                    </Box>
                    <Grid container>
                      <Grid item xs={6}>
                        <Typography variant="body2" color="text.secondary">
                          Total Funds
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography align="right" variant="body2">
                          {formattedStats.buy.totalFunds}
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography variant="body2" color="text.secondary">
                          Total Fee
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography align="right" variant="body2">
                          {formattedStats.buy.totalFee}
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography variant="body2" color="text.secondary">
                          Total Size
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography align="right" variant="body2">
                          {formattedStats.buy.totalSize}
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography variant="body2" color="text.secondary">
                          Avg Price
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography align="right" variant="body2">
                          {formattedStats.buy.avgPrice}
                        </Typography>
                      </Grid>
                    </Grid>
                  </Paper>
                </Grid>
                <Grid item xs={12} md={6}>
                  <Paper variant="outlined" sx={{ p: 2 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
                      <Chip label="SELL" color="error" size="small" />
                      <Typography variant="subtitle2" color="text.secondary">
                        Statistics
                      </Typography>
                    </Box>
                    <Grid container>
                      <Grid item xs={6}>
                        <Typography variant="body2" color="text.secondary">
                          Total Funds
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography align="right" variant="body2">
                          {formattedStats.sell.totalFunds}
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography variant="body2" color="text.secondary">
                          Total Fee
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography align="right" variant="body2">
                          {formattedStats.sell.totalFee}
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography variant="body2" color="text.secondary">
                          Total Size
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography align="right" variant="body2">
                          {formattedStats.sell.totalSize}
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography variant="body2" color="text.secondary">
                          Avg Price
                        </Typography>
                      </Grid>
                      <Grid item xs={6}>
                        <Typography align="right" variant="body2">
                          {formattedStats.sell.avgPrice}
                        </Typography>
                      </Grid>
                    </Grid>
                  </Paper>
                </Grid>
              </Grid>
            )}
          </Box>

          <TableContainer>
            <Table stickyHeader aria-label="trade history table">
              <TableHead>
                <TableRow>
                  {columns.map((column) => (
                    <TableCell
                      key={column.id}
                      align={column.align}
                      sx={{ minWidth: column.minWidth }}
                    >
                      {column.label}
                    </TableCell>
                  ))}
                </TableRow>
              </TableHead>
              <TableBody>
                {data?.items.map((trade) => (
                  <TableRow hover key={trade.tradeId}>
                    <TableCell align="left">
                      {new Date(trade.tradedAt).toLocaleString('en-US', DATE_FORMAT_OPTIONS)}
                    </TableCell>
                    <TableCell align="center">
                      <Chip
                        label={trade.side.toUpperCase()}
                        color={trade.side === 'buy' || trade.side === 'BUY' ? 'success' : 'error'}
                        size="small"
                      />
                    </TableCell>
                    <TableCell align="right">{trade.price.toFixed(2)}</TableCell>
                    <TableCell align="right">{trade.size.toFixed(8)}</TableCell>
                    <TableCell align="right">{trade.funds.toFixed(2)}</TableCell>
                    <TableCell align="right">{trade.fee.toFixed(4)}</TableCell>
                    <TableCell align="right">
                      <strong>{trade.total.toFixed(2)}</strong>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>

          {data && (
            <TablePagination
              rowsPerPageOptions={[10, 20, 50, 100]}
              component="div"
              count={data.totalCount}
              rowsPerPage={rowsPerPage}
              page={page}
              onPageChange={(_event, newPage) => setPage(newPage)}
              onRowsPerPageChange={(event) => {
                setRowsPerPage(parseInt(event.target.value, 10));
                setPage(0);
              }}
            />
          )}

          <BackdropLoading loading={isLoading} />
        </Paper>
      </Grid>
    </Grid>
  );
}
