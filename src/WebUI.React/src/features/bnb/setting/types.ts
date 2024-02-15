import { z } from 'zod';

export const UpdateSettingSchema = z.object({
  apiKey: z
    .string()
    .trim()
    .max(100, { message: 'Api Key  has reached a maximum of 100 characters.' }),
  secretKey: z
    .string()
    .trim()
    .max(100, { message: 'Secret Key  has reached a maximum of 100 characters.' }),
});

export type UpdateSettingData = z.infer<typeof UpdateSettingSchema>;
