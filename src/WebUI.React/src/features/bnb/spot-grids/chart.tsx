import { memo, useMemo } from 'react';
import { useSelector } from 'react-redux';
import Chart from 'components/chart';
import { RootState } from 'store';

function SpotGridChart(props: { pair: string; interval: string }) {
  const { pair, interval } = props;
  const { gridDetails, triggerPrice } = useSelector((state: RootState) => state.spotGridDetails);

  const gridLines = useMemo(
    () => gridDetails.map((item) => ({
      price: item.buyPrice,
      color: item.buyPrice < triggerPrice ? 'green' : 'red',
    })),
    [gridDetails, triggerPrice]
  );

  return <Chart pair={pair} interval={interval} gridLines={gridLines} />;
}

const MemoizedSpotGridChart = memo(SpotGridChart);
export default MemoizedSpotGridChart;
