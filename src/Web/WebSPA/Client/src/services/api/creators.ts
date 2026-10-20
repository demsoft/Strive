import Axios from 'axios';
import appSettings from 'src/config';

export type PaymentsConfig = {
   enabled: boolean;
   country: string;
   currency: string;
   platformCommissionPercent: number;
};

export type CreatorProfile = {
   creatorId: string;
   displayName: string;
   bio?: string | null;
   country: string;
   currency: string;
   bankCode?: string | null;
   bankName?: string | null;
   accountLast4?: string | null;
   accountName?: string | null;
   /** bank account verified and ready to be paid into */
   canSell: boolean;
   createdAt: string;
   /** what the 30-day pass costs, in minor units; none: no pass is sold */
   passPriceMinor?: number | null;
};

export type Bank = { code: string; name: string };

export type SaveCreatorRequest = {
   displayName: string;
   bio?: string;
   /** only when the bank account is new or changed */
   bankCode?: string;
   accountNumber?: string;
};

const base = () => `${appSettings.conferenceUrl.replace(/\/+$/, '')}/v1/creators`;

export async function fetchPaymentsConfig(): Promise<PaymentsConfig> {
   return (await Axios.get<PaymentsConfig>(`${base()}/config`)).data;
}

export async function fetchBanks(): Promise<Bank[]> {
   return (await Axios.get<Bank[]>(`${base()}/banks`)).data;
}

/** The name of the holder of the account, as the bank knows it. */
export async function resolveAccount(bankCode: string, accountNumber: string): Promise<string> {
   const response = await Axios.post<{ accountName: string }>(`${base()}/resolve-account`, { bankCode, accountNumber });
   return response.data.accountName;
}

/** null if the person is not a creator yet (a fan: the server answers with no content) */
export async function fetchMyCreatorProfile(): Promise<CreatorProfile | null> {
   try {
      const response = await Axios.get<CreatorProfile | ''>(`${base()}/me`);
      return response.status === 204 || !response.data ? null : response.data;
   } catch (error) {
      // an older server answered 404
      if ((error as { response?: { status?: number } }).response?.status === 404) return null;
      throw error;
   }
}

export async function saveMyCreatorProfile(request: SaveCreatorRequest): Promise<CreatorProfile> {
   return (await Axios.put<CreatorProfile>(`${base()}/me`, request)).data;
}

/** Sets what the pass costs, or stops selling it (null). */
export async function setPassPrice(priceMinor: number | null): Promise<CreatorProfile> {
   return (await Axios.put<CreatorProfile>(`${base()}/me/pass`, { priceMinor })).data;
}
