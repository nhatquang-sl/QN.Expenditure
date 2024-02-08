export interface Column {
  id: 'id' | 'ipAddress' | 'createdAt' | 'userAgent' | 'accessToken' | 'refreshToken';
  label: string;
  minWidth?: number;
  align?: 'right';
  format?: (value: string) => string;
}

const columns: readonly Column[] = [
  { id: 'id', label: 'Id' },
  { id: 'ipAddress', label: 'IP\u00a0Address', minWidth: 100 },
  {
    id: 'createdAt',
    label: 'Created At',
    minWidth: 170,
    align: 'right',
    format: (value: string) => new Date(value).toISOString(),
  },
  {
    id: 'accessToken',
    label: 'Access Token',
    minWidth: 170,
    align: 'right',
    format: (value: string) => `${value.slice(0, 5)}...${value.slice(-5)}`,
  },
  {
    id: 'refreshToken',
    label: 'Refresh Token',
    minWidth: 170,
    align: 'right',
    format: (value: string) => `${value.slice(0, 5)}...${value.slice(-5)}`,
  },
  {
    id: 'userAgent',
    label: 'User Agent',
    minWidth: 170,
    align: 'right',
  },
];

export { columns };
