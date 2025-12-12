import dayjs, { Dayjs } from 'dayjs';
import { z } from 'zod';

export const SyncSettingSchema = z.object({
  symbol: z
    .string()
    .trim()
    .min(1, { message: 'Symbol is required' })
    .max(50, { message: 'Symbol must not exceed 50 characters' }),
  startSync: z.custom<Dayjs>((val) => dayjs.isDayjs(val), {
    message: 'Start Sync must be a valid date',
  }),
});

export type SyncSettingData = z.infer<typeof SyncSettingSchema>;
