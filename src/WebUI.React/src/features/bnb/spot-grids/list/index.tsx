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
import { useQuery } from '@tanstack/react-query';
import { BackdropLoading } from 'components/backdrop-loading';

import { bnbSpotGridClient } from 'store';
export interface Column {
  id: 'symbol' | 'lastSyncAt' | 'action';
  label: string;
  minWidth?: number;
  align?: 'right';
  format?: (value: string) => string | JSX.Element;
}
const columns: readonly Column[] = [
  { id: 'symbol', label: 'Symbol' },
  {
    id: 'lastSyncAt',
    label: 'Last Sync At',
    align: 'right',
  },
  { id: 'action', label: 'Action', minWidth: 150, align: 'right' },
];

export default function BnbSpotGrids() {
  // Queries
  const { isLoading, data } = useQuery({
    queryKey: ['BnbSpotGrids'],
    queryFn: () => {
      bnbSpotGridClient.get();
    },
  });
  console.log(data);

  return (
    <Grid container spacing={3}>
      <Grid item xs={12}>
        <Paper sx={{ p: 2, display: 'flex', flexDirection: 'column', position: 'relative' }}>
          <TableContainer sx={{ flex: 1 }}>
            <Typography component="h2" variant="h6" color="primary" gutterBottom>
              Spot Grids
            </Typography>

            <Table stickyHeader aria-label="sticky table">
              <TableHead>
                <TableRow>
                  {columns.map((column) => (
                    <TableCell
                      key={column.id}
                      align={column.align}
                      style={{ width: column.minWidth }}
                    >
                      {column.label}
                    </TableCell>
                  ))}
                </TableRow>
              </TableHead>
              <TableBody>
                {/* {syncSettings &&
                  syncSettings.map((syncSetting) => {
                    return (
                      <SyncSettingItem
                        key={syncSetting.symbol}
                        syncSetting={syncSetting}
                        onDelete={deleteSyncSettingCallback}
                      />
                    );
                  })} */}
              </TableBody>
            </Table>
          </TableContainer>
          <BackdropLoading loading={isLoading} />
        </Paper>
      </Grid>
    </Grid>
  );
}
