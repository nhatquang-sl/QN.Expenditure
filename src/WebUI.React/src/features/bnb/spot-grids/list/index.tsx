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

import { Outlet } from 'react-router-dom';
import { bnbSpotGridClient } from 'store';
import SpotGridItem from './item';
import { columns } from './types';

export default function SpotGridList() {
  // Queries
  const { isLoading, data } = useQuery({
    queryKey: ['SpotGrids'],
    queryFn: async () => await bnbSpotGridClient.get(),
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
                      size="small"
                      style={{ width: column.width }}
                    >
                      {column.label}
                    </TableCell>
                  ))}
                </TableRow>
              </TableHead>
              <TableBody>
                {data &&
                  data.map((spotGrid) => {
                    return <SpotGridItem key={spotGrid.id} spotGrid={spotGrid} />;
                  })}
              </TableBody>
            </Table>
          </TableContainer>
          <Outlet />
          <BackdropLoading loading={isLoading} />
        </Paper>
      </Grid>
    </Grid>
  );
}
