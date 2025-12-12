export default class Kline {
  openTime: number;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  closeTime: number;
  quoteAssetVolume: number;
  numberOfTrades: number;
  takerBuyBaseAssetVolume: number;
  takerBuyQuoteAssetVolume: number;
  gain: number = 0;
  loss: number = 0;
  rsi: number = 0;
  sma20: number = 0;
  bolu: number = 0;
  bold: number = 0;

  constructor(kline: any[] = []) {
    this.openTime = kline[0];
    this.open = parseFloat(kline[1]);
    this.high = parseFloat(kline[2]);
    this.low = parseFloat(kline[3]);
    this.close = parseFloat(kline[4]);
    this.volume = parseFloat(kline[5]);
    this.closeTime = kline[6];
    this.quoteAssetVolume = parseFloat(kline[7]);
    this.numberOfTrades = kline[8];
    this.takerBuyBaseAssetVolume = parseFloat(kline[9]);
    this.takerBuyQuoteAssetVolume = parseFloat(kline[10]);
  }
}
