import { useState } from 'react';
import { useSelector, useDispatch } from 'react-redux';
import { increment, decrement, reset, incrementByAmount } from './slice';
import { RootState } from 'store';

const Counter = () => {
  const count = useSelector((state: RootState) => state.counter.count);
  const dispatch = useDispatch();

  const [incrementAmount, setIncrementAmount] = useState(0);

  const addValue = Number(incrementAmount) || 0;
  const resetAll = () => {
    setIncrementAmount(0);
    dispatch(reset());
  };

  return (
    <>
      <p>{count}</p>
      <div>
        <button onClick={() => dispatch(increment())}>+</button>
        <button onClick={() => dispatch(decrement())}>+</button>
      </div>
      <input
        type="text"
        value={incrementAmount}
        onChange={(e) => setIncrementAmount(Number(e.target.value) || 0)}
      />
      <div>
        <button onClick={() => dispatch(incrementByAmount(addValue))}>Add Amount</button>
        <button onClick={resetAll}>Reset</button>
      </div>
    </>
  );
};

export default Counter;
