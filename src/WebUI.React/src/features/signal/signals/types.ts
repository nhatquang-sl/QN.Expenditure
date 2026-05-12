export interface SignalDto {
  id: number;
  symbol: string;
  interval: string;
  signalType: string;
  detectedAt: string;
  rsiValue: number;
  previousRsiValue: number;
  entryPrice: number;
  stopLoss: number;
  takeProfit: number;
  leverage: number;
  maxProfit: number;
  maxProfitHitAt: string | null;
  entryHitAt: string | null;
  stopLossHitAt: string | null;
  takeProfitHitAt: string | null;
  createdAt: string;
}

export interface PaginatedList<T> {
  items: T[];
  pageNumber: number;
  totalPages: number;
  totalCount: number;
}

export const INTERVALS = ['1min', '5min', '15min', '30min', '1hour', '4hour', '1day'] as const;
export const SIGNAL_TYPES = ['Long', 'Short'] as const;
