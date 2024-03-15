import { z } from 'zod';

export const GridOrderSchema = z
  .object({
    symbol: z.string(),
    triggerPrice: z.number(),
    lowerPrice: z.number(),
    upperPrice: z.number(),
    numberOfGrids: z.number(),
    gridMode: z.number(),
    investment: z.number(),
    takeProfit: z.number(),
    stopLoss: z.number(),
  })
  .refine((data) => data.upperPrice > data.lowerPrice, {
    message: 'Upper Price must be greater than Lower Price',
    path: ['upperPrice'],
  });

export type GridOrderData = z.infer<typeof GridOrderSchema>;
