import { SpotGridMode } from 'store/api-client';
import { z } from 'zod';

export const GridOrderSchema = z
  .object({
    symbol: z.string(),
    triggerPrice: z.number(),
    lowerPrice: z.number(),
    upperPrice: z.number(),
    numberOfGrids: z.number(),
    gridMode: z.number().default(SpotGridMode.ARITHMETIC),
    investment: z.number(),
    takeProfit: z.coerce.number().nullable(),
    stopLoss: z.coerce.number().nullable(),
  })
  .refine((data) => data.upperPrice > data.lowerPrice, {
    message: 'Upper Price must be greater than Lower Price',
    path: ['upperPrice'],
  });

export type GridOrderData = z.infer<typeof GridOrderSchema>;
