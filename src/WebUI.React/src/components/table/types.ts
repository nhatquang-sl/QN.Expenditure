export type Column = {
  id: string;
  label: string;
  align?: 'inherit' | 'left' | 'center' | 'right' | 'justify';
};

export type TableDataProps = {
  id?: string;
  columns: Column[];
  data?: Record<string, unknown>[];
  isLoading: boolean;
  count: number;
  page: number;
  rowsPerPage: number;
  onPageChange?: (page: number, size: number) => void;
};
