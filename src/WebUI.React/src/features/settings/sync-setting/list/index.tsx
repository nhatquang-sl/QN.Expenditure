import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from '@mui/material';
import { SyncSettingDto } from 'store/api-client';
import SyncSettingItem from './item';
import { columns } from './types';

interface SyncSettingListProps {
  syncSettings?: SyncSettingDto[];
}

export default function SyncSettingList(props: SyncSettingListProps) {
  const { syncSettings } = props;

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
          {syncSettings &&
            syncSettings.map((syncSetting) => {
              return <SyncSettingItem key={syncSetting.symbol} syncSetting={syncSetting} />;
            })}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
