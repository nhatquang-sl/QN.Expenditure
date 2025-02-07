const fixedNumber = (value: number, fixed: number = 2): number => {
  if (value > 1) return parseFloat(value.toFixed(fixed));
  let pow = Math.pow(10, fixed);
  let newValue = Math.round((parseFloat(`${value}`) + Number.EPSILON) * pow);
  while (newValue < 10) {
    pow = Math.pow(10, fixed) * pow;
    newValue = Math.round((parseFloat(`${value}`) + Number.EPSILON) * pow);
    console.log({ newValue });
  }
  return newValue / pow;
};

const toKuCoinSymbol = (symbol: string) => {
  return symbol.replace('USDT', '-USDT');
};

export { fixedNumber, toKuCoinSymbol };
