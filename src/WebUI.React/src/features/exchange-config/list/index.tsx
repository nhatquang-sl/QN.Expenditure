import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from '@mui/material';
import { ExchangeConfigDto } from 'store/api-client';
import ExchangeConfigItem from './item';
import { columns } from './types';

interface ExchangeConfigListProps {
  exchangeConfigs?: ExchangeConfigDto[];
}
export default function ExchangeConfigList(props: ExchangeConfigListProps) {
  const { exchangeConfigs } = props;

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
          {exchangeConfigs &&
            exchangeConfigs.map((exchangeConfig) => {
              return (
                <ExchangeConfigItem
                  key={exchangeConfig.exchangeName}
                  exchangeConfig={exchangeConfig}
                />
              );
            })}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
