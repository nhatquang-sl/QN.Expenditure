import { ExchangeName } from 'store/api-client';
import { z } from 'zod';

export const ExchangeSettingSchema = z
  .object({
    exchangeName: z.nativeEnum(ExchangeName, {
      errorMap: () => ({
        message:
          'Invalid exchange name. Supported exchanges: Binance, KuCoin, Coinbase, Kraken, Bybit',
      }),
    }),
    apiKey: z
      .string()
      .trim()
      .min(1, { message: 'API Key is required' })
      .max(500, { message: 'API Key must not exceed 500 characters' }),
    secret: z
      .string({ required_error: 'Secret is required' })
      .trim()
      .min(1, { message: 'Secret is required' })
      .max(500, { message: 'Secret must not exceed 500 characters' }),
    passphrase: z
      .union([z.string(), z.null()])
      .transform((val) => {
        if (val === null || val === '') return undefined;
        return val;
      })
      .optional()
      .refine((val) => val === undefined || val.length <= 500, {
        message: 'Passphrase must not exceed 500 characters',
      }),
  })
  .superRefine((data, ctx) => {
    // Validate passphrase length
    if (data.passphrase !== undefined && data.passphrase.length > 500) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Passphrase must not exceed 500 characters',
        path: ['passphrase'],
      });
    }

    // KuCoin requires passphrase
    if (data.exchangeName === ExchangeName.KuCoin) {
      if (!data.passphrase || data.passphrase.length === 0) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: 'Passphrase is required for KuCoin',
          path: ['passphrase'],
        });
      }
    }
  });

export type ExchangeSettingData = z.infer<typeof ExchangeSettingSchema>;
