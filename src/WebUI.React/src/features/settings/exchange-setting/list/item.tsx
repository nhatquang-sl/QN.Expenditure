import { LoadingButton } from '@mui/lab';
import { Icon, TableCell, TableRow } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { ExchangeName, ExchangeSettingDto } from 'store/api-client';
import { useDeleteExchangeSetting } from '../hooks/use-delete-exchange-setting';
import { columns } from './types';

interface ExchangeSettingItemProps {
  exchangeSetting: ExchangeSettingDto;
}
export default function ExchangeSettingItem(props: ExchangeSettingItemProps) {
  const { exchangeSetting } = props;

  const navigate = useNavigate();
  const { mutate: deleteSetting, isPending } = useDeleteExchangeSetting();

  const handleEdit = (exchangeName: ExchangeName) => {
    navigate(`/settings/exchange-setting/${exchangeName}`);
  };

  const handleDelete = (exchangeName: ExchangeName) => {
    deleteSetting(exchangeName);
  };

  return (
    <TableRow key={exchangeSetting.exchangeName}>
      {columns.map((column) => {
        if (column.id === 'actions') {
          return (
            <TableCell key={column.id} align={column.align}>
              <LoadingButton
                aria-label="edit"
                onClick={() => handleEdit(exchangeSetting.exchangeName)}
                size="small"
                sx={{ minWidth: 'auto', p: 1, color: 'action.active' }}
              >
                <Icon>edit</Icon>
              </LoadingButton>
              <LoadingButton
                aria-label="delete"
                onClick={() => handleDelete(exchangeSetting.exchangeName)}
                size="small"
                loading={isPending}
                sx={{ minWidth: 'auto', p: 1, color: 'action.active' }}
              >
                <Icon>delete</Icon>
              </LoadingButton>
            </TableCell>
          );
        }
        const value = exchangeSetting[column.id];
        return (
          <TableCell key={column.id} align={column.align}>
            {column.format && value != null ? column.format(String(value)) : value ?? '-'}
          </TableCell>
        );
      })}
    </TableRow>
  );
}
