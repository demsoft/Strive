/** Nigerian bank account numbers (NUBAN) have ten digits. */
export const ACCOUNT_NUMBER_LENGTH = 10;

/** Only digits, at most ten (people paste numbers with spaces and dashes). */
export const cleanAccountNumber = (input: string) => input.replace(/\D/g, '').slice(0, ACCOUNT_NUMBER_LENGTH);

export const isCompleteAccountNumber = (input: string) => input.length === ACCOUNT_NUMBER_LENGTH && /^\d+$/.test(input);
