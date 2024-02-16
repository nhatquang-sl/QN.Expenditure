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
  { id: 'status', label: 'Status' },
  { id: 'executedQty', label: 'Executed Qty', format: (value) => parseFloat(value).toString() },
  { id: 'price', label: 'Price', format: (value) => parseFloat(value).toString() },
  { id: 'cummulativeQuoteQty', label: 'Total', format: (value) => parseFloat(value).toString() },
];

export { columns };
