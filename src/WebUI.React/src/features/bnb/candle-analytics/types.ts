export type DayCell = {
  date: Date;
  percentage: number;
  direction: 'up' | 'down' | 'flat';
};

export type MonthGroup = {
  year: number;
  month: number; // 0-indexed
  cells: (DayCell | null)[];
};

export type ColorScale = {
  min: number;
  bucketSize: number;
};
