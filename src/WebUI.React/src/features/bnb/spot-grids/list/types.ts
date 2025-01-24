export interface Column {
  id: string;
  label: string;
  width?: string;
  align?: 'right';
  format?: (value: string) => string | JSX.Element;
}
const columns: readonly Column[] = [
  { id: 'symbol', label: 'Symbol' },
  {
    id: 'investment',
    label: 'Investment',
    align: 'right',
  },
  { id: 'totalProfit', label: 'Total Profit', align: 'right' },
  { id: 'gridProfit', label: 'Grid Profit', align: 'right' },
  { id: 'unrealizedPNL', label: 'Unrealized PNL', align: 'right' },
  { id: 'price', label: 'Price', align: 'right', width: '10%' },
  { id: 'entry', label: 'Entry', align: 'right' },
  { id: 'range', label: 'Range', align: 'right' },
  { id: 'action', label: 'Action', align: 'right', width: '60px' },
];

export { columns };
