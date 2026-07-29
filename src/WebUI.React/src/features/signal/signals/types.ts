export interface SignalDto {
  id: number;
  symbol: string;
  interval: string;
  signalType: string;
  detectedAt: number;
  rsiValue: number;
  previousRsiValue: number;
  entryPrice: number;
  stopLoss: number;
  takeProfit: number;
  leverage: number;
  maxProfit: number;
  maxProfitHitAt: number | null;
  entryHitAt: number | null;
  stopLossHitAt: number | null;
  takeProfitHitAt: number | null;
  createdAt: number;
  entryHitAfterMinutes: number;
  maxProfitHitAfterMinutes: number;
  stopLossHitAfterMinutes: number;
}

export interface PaginatedList<T> {
  items: T[];
  pageNumber: number;
  totalPages: number;
  totalCount: number;
}

export const INTERVALS = ['1min', '5min', '15min', '30min', '1hour', '4hour', '1day'] as const;
export const SIGNAL_TYPES = ['Long', 'Short'] as const;

export interface SignalStatisticInfo {
  totalSignals: number;
  totalEntries: number;
  totalMaxProfitHits: number;
  totalStopLossHits: number;
  avgEntryPrice: number;
  avgMaxProfit: number;
}

export interface SignalStatistics {
  today: SignalStatisticInfo;
  yesterday: SignalStatisticInfo;
  thisWeek: SignalStatisticInfo;
  lastWeek: SignalStatisticInfo;
  thisMonth: SignalStatisticInfo;
  lastMonth: SignalStatisticInfo;
}
