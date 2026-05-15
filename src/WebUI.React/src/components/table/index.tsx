import React from 'react';
import { TableDataProps } from './types';
import {
  TableContainer,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
  TablePagination,
} from '@mui/material';

export default function TableData(props: TableDataProps) {
  const { id, columns, data, isLoading, count, page, rowsPerPage } = props;

  return (
    <>
      <TableContainer sx={{ flex: 1, minHeight: 0, overflow: 'auto' }}>
        <Table stickyHeader size="small" aria-label={id ?? 'table-data'}>
          <TableHead>
            <TableRow>
              {columns.map((col) => (
                <TableCell key={col.id} align={col.align}>
                  {col.label}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading && !data && (
              <TableRow>
                <TableCell colSpan={columns.length} align="center">
                  <Typography variant="body2" color="textSecondary" sx={{ py: 4 }}>
                    Loading...
                  </Typography>
                </TableCell>
              </TableRow>
            )}
            {data?.length === 0 && (
              <TableRow>
                <TableCell colSpan={columns.length} align="center">
                  <Typography variant="body2" color="textSecondary" sx={{ py: 4 }}>
                    No data available.
                  </Typography>
                </TableCell>
              </TableRow>
            )}
            {data?.map((row, rowIndex) => (
              <TableRow key={rowIndex}>
                {columns.map((col) => (
                  <TableCell key={col.id} align={col.align}>
                    {row[col.id] as React.ReactNode}
                  </TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <TablePagination
        rowsPerPageOptions={[10, 20, 50, 100]}
        component="div"
        count={count}
        rowsPerPage={rowsPerPage}
        page={page}
        onPageChange={(_event, newPage) => {
          props.onPageChange?.(newPage, rowsPerPage);
        }}
        onRowsPerPageChange={(event) => {
          const size = parseInt(event.target.value, 10);
          props.onPageChange?.(0, size);
        }}
      />
    </>
  );
}
