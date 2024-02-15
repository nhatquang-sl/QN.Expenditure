import { DateTimePicker } from '@mui/x-date-pickers';
import { Dayjs } from 'dayjs';
import { SpotOrderSyncSettingDto } from 'store/api-client';
import { z } from 'zod';

export type OnChangeCallback = (syncSetting: SpotOrderSyncSettingDto) => void;

export interface Column {
  id: 'symbol' | 'lastSyncAt' | 'action';
  label: string;
  minWidth?: number;
  align?: 'right';
  format?: (value: string) => string | JSX.Element;
}

const columns: readonly Column[] = [
  { id: 'symbol', label: 'Symbol' },
  {
    id: 'lastSyncAt',
    label: 'Last Sync At',
    align: 'right',
    format: (value: string) => (
      <DateTimePicker defaultValue={new Date(parseInt(value)).toISOString()} />
    ),
  },
  { id: 'action', label: 'Action', minWidth: 10, align: 'right' },
];

export { columns };

export const UpdateSpotOrderSchema = z.object({
  lastSyncAt: z.custom<Dayjs>(),
});

export const CreateSpotOrderSchema = UpdateSpotOrderSchema.extend({
  symbol: z
    .string()
    .trim()
    .toUpperCase()
    .min(6, { message: 'Symbol must be at least 6 characters.' })
    .max(10, { message: 'Symbol has reached a maximum of 10 characters.' }),
});

export type UpdateSpotOrderData = z.infer<typeof UpdateSpotOrderSchema>;
export type CreateSpotOrderData = z.infer<typeof CreateSpotOrderSchema>;
