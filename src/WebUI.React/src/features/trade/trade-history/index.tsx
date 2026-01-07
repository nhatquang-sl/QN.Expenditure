import { ArrowBack } from '@mui/icons-material';
import {
    Box,
    Button,
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
    Typography,
} from '@mui/material';
import { BackdropLoading } from 'components/backdrop-loading';
import { setTitle } from 'features/layout/slice';
import { useEffect, useState } from 'react';
import { useDispatch } from 'react-redux';
import { useNavigate, useParams } from 'react-router-dom';
import { useGetTradeHistories } from './hooks/use-get-trade-histories';
import { columns } from './types';

export default function TradeHistory() {
  const { symbol } = useParams<{ symbol: string }>();
  const navigate = useNavigate();
  const dispatch = useDispatch();
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(20);

  const { data, isLoading } = useGetTradeHistories(symbol || '', page + 1, rowsPerPage);

  useEffect(() => {dispatch(setTitle('Trade History'));}, [dispatch]);

  const handleChangePage = (_event: unknown, newPage: number) => {
    setPage(newPage);
  };

  const handleChangeRowsPerPage = (event: React.ChangeEvent<HTMLInputElement>) => {
    setRowsPerPage(parseInt(event.target.value, 10));
    setPage(0);
  };

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
              <Typography component="h2" variant="h6" color="primary">
                {symbol}
              </Typography>
            </Box>
            {data && (
              <Typography variant="body2" color="textSecondary">
                Total: {data.totalCount} trades
              </Typography>
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
                      style={{ minWidth: column.minWidth }}
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
                      {new Date(trade.tradedAt).toLocaleString('en-US', {
                        year: 'numeric',
                        month: 'long',
                        day: 'numeric',
                        hour: '2-digit',
                        minute: '2-digit',
                        second: '2-digit',
                        hour12: false,
                      })}
                    </TableCell>
                    <TableCell align="center">
                      <Chip
                        label={trade.side.toUpperCase()}
                        color={trade.side.toLowerCase() === 'buy' ? 'success' : 'error'}
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
              onPageChange={handleChangePage}
              onRowsPerPageChange={handleChangeRowsPerPage}
            />
          )}

          <BackdropLoading loading={isLoading} />
        </Paper>
      </Grid>
    </Grid>
  );
}
