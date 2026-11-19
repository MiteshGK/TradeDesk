export type TradeSide = 'Lend' | 'Borrow';
export type TradeStatus = 'Booked' | 'Settled' | 'Matured' | 'Cancelled';

export interface Trade {
  id: number;
  tradeRef: string;
  counterparty: string;
  side: TradeSide;
  principal: number;
  ratePercent: number;
  tradeDate: string;
  startDate: string;
  maturityDate: string;
  tenorDays: number;
  interest: number;
  maturityAmount: number;
  status: TradeStatus;
}

export interface BookTradeRequest {
  counterparty: string;
  side: TradeSide;
  principal: number;
  ratePercent: number;
  tradeDate: string;
  startDate: string;
  maturityDate: string;
}

export interface CounterpartyExposure {
  counterparty: string;
  netLent: number;
  openTrades: number;
}

export interface BlotterSummary {
  openTrades: number;
  totalLent: number;
  totalBorrowed: number;
  netPosition: number;
  weightedAverageRate: number;
  interestReceivable: number;
  interestPayable: number;
  topExposures: CounterpartyExposure[];
}

export type TradeAction = 'settle' | 'mature' | 'cancel';

/** Mirrors TradeCalculator on the server: Actual/365 simple interest, rounded to 2 decimals. */
export function previewInterest(principal: number, ratePercent: number, tenorDays: number): number {
  if (principal <= 0 || tenorDays <= 0) return 0;
  return Math.round(((principal * ratePercent) / 100) * (tenorDays / 365) * 100) / 100;
}
