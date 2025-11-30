import { LoadingButton } from '@mui/lab';
import { Icon, TableCell, TableRow } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { ExchangeConfigDto, ExchangeName } from 'store/api-client';
import { useDeleteExchangeConfig } from '../hooks/use-delete-exchange-config';
import { columns } from './types';

interface ExchangeConfigItemProps {
  exchangeConfig: ExchangeConfigDto;
}
export default function ExchangeConfigItem(props: ExchangeConfigItemProps) {
  const { exchangeConfig } = props;

  const navigate = useNavigate();
  const { mutate: deleteConfig, isPending } = useDeleteExchangeConfig();

  const handleEdit = (exchangeName: ExchangeName) => {
    navigate(`/exchange-config/${exchangeName}`);
  };

  const handleDelete = (exchangeName: ExchangeName) => {
    deleteConfig(exchangeName);
  };

  return (
    <TableRow key={exchangeConfig.exchangeName}>
      {columns.map((column) => {
        if (column.id === 'actions') {
          return (
            <TableCell key={column.id} align={column.align}>
              <LoadingButton
                aria-label="edit"
                onClick={() => handleEdit(exchangeConfig.exchangeName)}
                size="small"
                sx={{ minWidth: 'auto', p: 1, color: 'action.active' }}
              >
                <Icon>edit</Icon>
              </LoadingButton>
              <LoadingButton
                aria-label="delete"
                onClick={() => handleDelete(exchangeConfig.exchangeName)}
                size="small"
                loading={isPending}
                sx={{ minWidth: 'auto', p: 1, color: 'action.active' }}
              >
                <Icon>delete</Icon>
              </LoadingButton>
            </TableCell>
          );
        }
        const value = exchangeConfig[column.id];
        return (
          <TableCell key={column.id} align={column.align}>
            {column.format && value != null ? column.format(String(value)) : value ?? '-'}
          </TableCell>
        );
      })}
    </TableRow>
  );
}
