import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from '@mui/material';
import { ExchangeSettingDto } from 'store/api-client';
import ExchangeSettingItem from './item';
import { columns } from './types';

interface ExchangeSettingListProps {
  exchangeSettings?: ExchangeSettingDto[];
}
export default function ExchangeSettingList(props: ExchangeSettingListProps) {
  const { exchangeSettings } = props;

  return (
    <TableContainer sx={{ flex: 1, mt: 3 }}>
      <Table stickyHeader aria-label="sticky table">
        <TableHead>
          <TableRow>
            {columns.map((column) => (
              <TableCell key={column.id} align={column.align} style={{ width: column.minWidth }}>
                {column.label}
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {exchangeSettings &&
            exchangeSettings.map((exchangeSetting) => {
              return (
                <ExchangeSettingItem
                  key={exchangeSetting.exchangeName}
                  exchangeSetting={exchangeSetting}
                />
              );
            })}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
