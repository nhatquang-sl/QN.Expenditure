import {
  Grid,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useCallback, useEffect, useState } from 'react';
import { authClient } from 'store';
import { UserLoginHistory } from 'store/api-client';
import { PAGE } from 'store/constants';
import SessionRow from './session-row';
import { columns } from './types';
// import sessionsApi from 'store/sessions-api';
// import { setSessionsPage } from 'store/sessions-slice';

const LoginHistory = () => {
  const [isLoading, setIsLoading] = useState(false);
  const [sessions, setSessions] = useState<UserLoginHistory[]>([]);

  const fetchSessions = useCallback(async (page: number = PAGE.START, size: number = PAGE.SIZE) => {
    setIsLoading(true);
    var loginHistories = await authClient.getLoginHistories(page, size);
    setSessions(loginHistories);
    console.log(loginHistories);
    setIsLoading(false);
  }, []);

  useEffect(() => {
    console.log('LoginHistory');

    fetchSessions();
  }, [fetchSessions]);

  return (
    <Grid container spacing={3}>
      {/* Recent Orders */}
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column' }}>
          <TableContainer sx={{ flex: 1 }}>
            <Typography component="h2" variant="h6" color="primary" gutterBottom>
              Login Histories
            </Typography>
            <Table stickyHeader aria-label="sticky table">
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
                {sessions.map((session) => {
                  return <SessionRow key={session.id} session={session} />;
                })}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      </Grid>
    </Grid>
  );
};

export default LoginHistory;
