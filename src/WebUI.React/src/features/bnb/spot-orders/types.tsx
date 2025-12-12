import { round4Dec } from 'store/constants';

export interface Column {
  id: 'symbol' | 'side' | 'status' | 'executedQty' | 'price' | 'cummulativeQuoteQty';
  label: string;
  minWidth?: number;
  align?: 'right';
  format?: (value: string) => string | JSX.Element;
}

const columns: readonly Column[] = [
  { id: 'symbol', label: 'Symbol' },
  { id: 'side', label: 'Side' },
  { id: 'status', label: 'Buy New' },
  { id: 'executedQty', label: 'Executed Qty', format: (value) => parseFloat(value).toString() },
  { id: 'price', label: 'Price', format: (value) => parseFloat(value).toString() },
  {
    id: 'cummulativeQuoteQty',
    label: 'Cum Quote Qty',
    format: (value) => parseFloat(value).toString(),
  },
];
export { columns };

export interface SummaryColumn {
  id:
    | 'symbol'
    | 'buy'
    | 'buyAvgPrice'
    | 'buyNew'
    | 'sell'
    | 'sellNew'
    | 'cumBuy'
    | 'cumSell'
    | 'profit'
    | 'lastSyncAt';
  label: string;
  minWidth?: number;
  align?: 'right';
  format?: (value: string | number) => string | JSX.Element;
}
export const summaryColumns: readonly SummaryColumn[] = [
  { id: 'symbol', label: 'Symbol' },
  { id: 'buy', label: 'Buy', format: (value) => round4Dec(value).toString() },
  { id: 'buyAvgPrice', label: 'Avg Price', format: (value) => round4Dec(value).toString() },
  { id: 'buyNew', label: 'Buy New' },
  { id: 'sell', label: 'Sell', format: (value) => parseFloat(value as string).toString() },
  { id: 'sellNew', label: 'Sell New', format: (value) => parseFloat(value as string).toString() },
  {
    id: 'cumBuy',
    label: 'Cum Buy Quote',
    format: (value) => round4Dec(value).toString(),
  },
  {
    id: 'cumSell',
    label: 'Cum Sell Quote',
    format: (value) => round4Dec(value).toString(),
  },
  {
    id: 'profit',
    label: 'Profit',
    format: (value) => round4Dec(value).toString(),
  },
  {
    id: 'lastSyncAt',
    label: 'Last Sync At',
    format: (value) => new Date(value).toISOString(),
  },
];
