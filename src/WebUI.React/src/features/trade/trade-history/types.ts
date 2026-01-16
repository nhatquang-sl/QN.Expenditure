import { TradeHistoryDto } from 'store/api-client';

export interface Column {
  id: keyof TradeHistoryDto;
  label: string;
  minWidth?: number;
  align?: 'right' | 'left' | 'center';
  format?: (value: TradeHistoryDto) => string;
}

export const columns: readonly Column[] = [
  {
    id: 'tradedAt',
    label: 'Traded At',
    minWidth: 180,
    align: 'left',
    format: (value: TradeHistoryDto) =>
      value.tradedAt.toLocaleString('en-US', {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false,
      }),
  },
  {
    id: 'side',
    label: 'Side',
    minWidth: 70,
    align: 'center',
  },
  {
    id: 'price',
    label: 'Price',
    minWidth: 100,
    align: 'right',
    format: (value: TradeHistoryDto) => value.price.toFixed(2),
  },
  {
    id: 'size',
    label: 'Size',
    minWidth: 100,
    align: 'right',
    format: (value: TradeHistoryDto) => value.size.toFixed(8),
  },
  {
    id: 'funds',
    label: 'Funds',
    minWidth: 100,
    align: 'right',
    format: (value: TradeHistoryDto) => value.funds.toFixed(2),
  },
  {
    id: 'fee',
    label: 'Fee',
    minWidth: 80,
    align: 'right',
    format: (value: TradeHistoryDto) => value.fee.toFixed(4),
  },
  {
    id: 'total',
    label: 'Total',
    minWidth: 100,
    align: 'right',
    format: (value: TradeHistoryDto) => value.total.toFixed(2),
  },
];
