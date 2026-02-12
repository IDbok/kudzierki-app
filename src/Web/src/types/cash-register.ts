export type CashRegisterClosingRequest = {
  cashBalanceFact: number;
  terminalIncomeFact: number;
  comment?: string | null;
  cashIncomeAltegio?: number | null;
  transferIncomeAltegio?: number | null;
  terminalIncomeAltegio?: number | null;
  cashSpendingAdmin?: number | null;
  inCashTransfer?: number | null;
  outCashTransfer?: number | null;
};

export type CashRegisterClosingResponse = {
  date: string;
  cashBalanceFact: number;
  terminalIncomeFact: number;
  dayBefore: string | null;
  cashBalanceDayBefore: number;
  cashIncomeFact: number;
  cashIncomeAltegio: number;
  cashIncomeDifference: number;
  terminalIncomeAltegio: number;
  terminalIncomeDifference: number;
  transferIncomeAltegio: number;
  cashSpendingAdmin: number;
  inCashTransfer: number;
  outCashTransfer: number;
  isCashConfirmed: boolean;
  isTerminalConfirmed: boolean;
  comment?: string | null;
};
