export interface Column {
  id: 'symbol' | 'startSync' | 'lastSync' | 'actions';
  label: string;
  minWidth?: number;
  align?: 'right' | 'left' | 'center';
  format?: (value: number) => string;
}

export const columns: readonly Column[] = [
  {
    id: 'symbol',
    label: 'Symbol',
    minWidth: 100,
  },
  {
    id: 'startSync',
    label: 'Start Sync',
    minWidth: 150,
    format: (value: number) => new Date(value).toLocaleString(),
  },
  {
    id: 'lastSync',
    label: 'Last Sync',
    minWidth: 150,
    format: (value: number) => new Date(value).toLocaleString(),
  },
  {
    id: 'actions',
    label: 'Actions',
    minWidth: 100,
    align: 'center',
  },
];
