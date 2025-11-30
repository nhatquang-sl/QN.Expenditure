import { ExchangeName } from 'store/api-client';

export interface Column {
  id: 'exchangeName' | 'apiKey' | 'secret' | 'passphrase' | 'actions';
  label: string;
  minWidth?: number;
  align?: 'right' | 'left' | 'center';
  format?: (value: string) => string;
}

export const columns: readonly Column[] = [
  {
    id: 'exchangeName',
    label: 'Exchange',
    minWidth: 100,
    format: (value: string) => ExchangeName[Number(value) as ExchangeName],
  },
  {
    id: 'apiKey',
    label: 'API Key',
    minWidth: 150,
    format: (value: string) =>
      value && value.length > 12 ? `${value.slice(0, 8)}...${value.slice(-4)}` : value,
  },
  {
    id: 'secret',
    label: 'Secret',
    minWidth: 150,
    format: (value: string) =>
      value && value.length > 12 ? `${value.slice(0, 8)}...${value.slice(-4)}` : value,
  },
  {
    id: 'passphrase',
    label: 'Passphrase',
    minWidth: 120,
    format: (value: string) =>
      value && value.length > 6 ? `${value.slice(0, 4)}...${value.slice(-2)}` : value || '-',
  },
  {
    id: 'actions',
    label: 'Actions',
    minWidth: 100,
    align: 'center',
  },
];
