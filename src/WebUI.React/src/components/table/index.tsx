import React from 'react';
import { TableDataProps } from './types';
import {
  TableContainer,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TableSortLabel,
  Typography,
  TablePagination,
} from '@mui/material';

export default function TableData(props: TableDataProps) {
  const { id, columns, data, isLoading, count, page, rowsPerPage, sortBy, sortOrder, onSortChange } = props;

  const handleHeaderClick = (colId: string) => {
    if (!onSortChange) return;
    if (sortBy === colId) {
      sortOrder === 'asc' ? onSortChange(colId, 'desc') : onSortChange('', 'desc');
    } else {
      onSortChange(colId, 'asc');
    }
  };

  return (
    <>
      <TableContainer sx={{ flex: 1, minHeight: 0, overflow: 'auto' }}>
        <Table stickyHeader size="small" aria-label={id ?? 'table-data'}>
          <TableHead>
            <TableRow>
              {columns.map((col) => (
                <TableCell key={col.id} align={col.align}>
                  {col.sortable ? (
                    <TableSortLabel
                      active={sortBy === col.id}
                      direction={sortBy === col.id ? (sortOrder ?? 'asc') : 'asc'}
                      onClick={() => handleHeaderClick(col.id)}
                      sx={{ '& .MuiTableSortLabel-icon': { opacity: sortBy === col.id ? 1 : 0.3 } }}
                    >
                      {col.label}
                    </TableSortLabel>
                  ) : (
                    col.label
                  )}
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
              <TableRow key={String(row['id'] ?? rowIndex)}>
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
