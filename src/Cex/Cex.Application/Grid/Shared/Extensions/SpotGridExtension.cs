using Cex.Domain.Entities;
using Lib.Application.Extensions;

namespace Cex.Application.Grid.Shared.Extensions
{
    public static class SpotGridExtension
    {
        private const decimal InitialPercent = 0.25m;

        private static decimal GetInitStepQty(this SpotGrid grid)
        {
            return (InitialPercent * grid.QuoteBalance / grid.TriggerPrice).FixedNumber();
        }

        public static void AddOrUpdateInitialStep(this SpotGrid grid, SpotGridStep? initStep = null)
        {
            if (initStep == null)
            {
                grid.GridSteps.Add(new SpotGridStep
                {
                    BuyPrice = grid.TriggerPrice.FixedNumber(),
                    SellPrice = grid.TriggerPrice.FixedNumber(),
                    Qty = grid.GetInitStepQty(),
                    Status = SpotGridStepStatus.AwaitingBuy,
                    Type = SpotGridStepType.Initial
                });

                return;
            }

            // Trigger Price unchanged
            if (initStep.BuyPrice == grid.TriggerPrice.FixedNumber())
            {
                return;
            }

            initStep.BuyPrice = grid.TriggerPrice.FixedNumber();
            initStep.SellPrice = grid.TriggerPrice.FixedNumber();
            initStep.Qty = grid.GetInitStepQty();
            initStep.Status = SpotGridStepStatus.AwaitingBuy;
            initStep.OrderId = null;
        }

        public static void AddTakeProfitStep(this SpotGrid entity)
        {
            if (!entity.TakeProfit.HasValue)
            {
                return;
            }

            entity.GridSteps.Add(new SpotGridStep
            {
                BuyPrice = entity.TakeProfit.Value.FixedNumber(),
                SellPrice = entity.TakeProfit.Value.FixedNumber(),
                Qty = 0,
                Status = SpotGridStepStatus.AwaitingBuy,
                Type = SpotGridStepType.TakeProfit
            });
        }

        public static void AddStopLossStep(this SpotGrid entity)
        {
            if (!entity.StopLoss.HasValue)
            {
                return;
            }

            entity.GridSteps.Add(new SpotGridStep
            {
                BuyPrice = entity.StopLoss.Value.FixedNumber(),
                SellPrice = entity.StopLoss.Value.FixedNumber(),
                Qty = 0,
                Status = SpotGridStepStatus.AwaitingBuy,
                Type = SpotGridStepType.StopLoss
            });
        }

        public static void AddNormalSteps(this SpotGrid entity)
        {
            var stepSize = (entity.UpperPrice - entity.LowerPrice) / entity.NumberOfGrids;
            var investmentPerStep = (1 - InitialPercent) * entity.QuoteBalance / entity.NumberOfGrids;
            for (var i = 0; i < entity.NumberOfGrids; i++)
            {
                entity.GridSteps.Add(new SpotGridStep
                {
                    BuyPrice = (entity.LowerPrice + stepSize * i).FixedNumber(),
                    SellPrice = (entity.LowerPrice + stepSize * (i + 1)).FixedNumber(),
                    Qty = (investmentPerStep / (entity.LowerPrice + stepSize * i)).FixedNumber(),
                    Status = SpotGridStepStatus.AwaitingBuy,
                    Type = SpotGridStepType.Normal
                });
            }
        }

        public static void UpdateOrDeleteTakeProfitStep(this SpotGrid entity, SpotGridStep takeProfitStep,
            DateTime currentDateTime)
        {
            takeProfitStep.OrderId = null;
            if (entity.TakeProfit.HasValue)
            {
                takeProfitStep.BuyPrice = entity.TakeProfit.Value.FixedNumber();
                takeProfitStep.SellPrice = entity.TakeProfit.Value.FixedNumber();
                takeProfitStep.Status = SpotGridStepStatus.AwaitingBuy;
            }
            else
            {
                takeProfitStep.DeletedAt = currentDateTime;
            }
        }

        public static void UpdateOrDeleteStopLossStep(this SpotGrid entity, SpotGridStep takeProfitStep,
            DateTime currentDateTime)
        {
            takeProfitStep.OrderId = null;
            if (entity.StopLoss.HasValue)
            {
                takeProfitStep.BuyPrice = entity.StopLoss.Value.FixedNumber();
                takeProfitStep.SellPrice = entity.StopLoss.Value.FixedNumber();
                takeProfitStep.Status = SpotGridStepStatus.AwaitingBuy;
            }
            else
            {
                takeProfitStep.DeletedAt = currentDateTime;
            }
        }
    }
}