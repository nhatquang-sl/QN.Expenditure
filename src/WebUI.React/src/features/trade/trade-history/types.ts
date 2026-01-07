export interface Column {
  id: 'tradedAt' | 'side' | 'price' | 'size' | 'funds' | 'fee' | 'total';
  label: string;
  minWidth?: number;
  align?: 'right' | 'left' | 'center';
  format?: (value: any) => string;
}

export const columns: readonly Column[] = [
  {
    id: 'tradedAt',
    label: 'Traded At',
    minWidth: 180,
    align: 'left',
    format: (value: Date) =>
      new Date(value).toLocaleString('en-US', {
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
    format: (value: number) => value.toFixed(2),
  },
  {
    id: 'size',
    label: 'Size',
    minWidth: 100,
    align: 'right',
    format: (value: number) => value.toFixed(8),
  },
  {
    id: 'funds',
    label: 'Funds',
    minWidth: 100,
    align: 'right',
    format: (value: number) => value.toFixed(2),
  },
  {
    id: 'fee',
    label: 'Fee',
    minWidth: 80,
    align: 'right',
    format: (value: number) => value.toFixed(4),
  },
  {
    id: 'total',
    label: 'Total',
    minWidth: 100,
    align: 'right',
    format: (value: number) => value.toFixed(2),
  },
];
