import { LoadingButton } from '@mui/lab';
import { Icon, TableCell, TableRow } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { SyncSettingDto } from 'store/api-client';
import { useDeleteSyncSetting } from '../hooks/use-delete-sync-setting';
import { useSyncTradeHistoryBySymbol } from '../hooks/use-sync-trade-history-by-symbol';
import { columns } from './types';

interface SyncSettingItemProps {
  syncSetting: SyncSettingDto;
}

export default function SyncSettingItem(props: SyncSettingItemProps) {
  const { syncSetting } = props;

  const navigate = useNavigate();
  const { mutate: deleteSetting, isPending: isDeleting } = useDeleteSyncSetting();
  const { mutate: syncTrades, isPending: isSyncing } = useSyncTradeHistoryBySymbol();

  const handleEdit = (symbol: string) => {
    navigate(`/sync/sync-setting/${symbol}`);
  };

  const handleDelete = (symbol: string) => {
    deleteSetting(symbol);
  };

  const handleSync = (symbol: string) => {
    syncTrades(symbol);
  };

  return (
    <TableRow key={syncSetting.symbol}>
      {columns.map((column) => {
        if (column.id === 'actions') {
          return (
            <TableCell key={column.id} align={column.align}>
              <LoadingButton
                aria-label="sync"
                onClick={() => handleSync(syncSetting.symbol)}
                size="small"
                loading={isSyncing}
                sx={{ minWidth: 'auto', p: 1, color: 'action.active' }}
              >
                <Icon>sync</Icon>
              </LoadingButton>
              <LoadingButton
                aria-label="edit"
                onClick={() => handleEdit(syncSetting.symbol)}
                size="small"
                sx={{ minWidth: 'auto', p: 1, color: 'action.active' }}
              >
                <Icon>edit</Icon>
              </LoadingButton>
              <LoadingButton
                aria-label="delete"
                onClick={() => handleDelete(syncSetting.symbol)}
                size="small"
                loading={isDeleting}
                sx={{ minWidth: 'auto', p: 1, color: 'action.active' }}
              >
                <Icon>delete</Icon>
              </LoadingButton>
            </TableCell>
          );
        }
        const value = syncSetting[column.id];
        return (
          <TableCell key={column.id} align={column.align}>
            {column.format ? column.format(value as number) : value}
          </TableCell>
        );
      })}
    </TableRow>
  );
}
