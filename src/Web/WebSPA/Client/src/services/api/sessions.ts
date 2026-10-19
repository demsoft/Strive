import Axios from 'axios';
import appSettings from 'src/config';

export type SessionStatus = 'published' | 'cancelled';

export type Session = {
   sessionId: string;
   conferenceId: string;
   title: string;
   description?: string | null;
   category: string;
   /** in minor units (kobo): 500000 is 5,000 naira */
   priceMinor: number;
   currency: string;
   status: SessionStatus;
   startsAt?: string | null;
   createdAt: string;
};

export type PublicSession = {
   sessionId: string;
   title: string;
   description?: string | null;
   category: string;
   priceMinor: number;
   currency: string;
   status: SessionStatus;
   startsAt?: string | null;
   creator: { creatorId: string; displayName: string; bio?: string | null };
   isHost: boolean;
   hasTicket: boolean;
   /** only for the host and people with a ticket */
   conferenceId?: string | null;
};

export type CreateSessionRequest = {
   title: string;
   description?: string;
   category: string;
   priceMinor: number;
   startsAt?: string;
};

export type CheckoutResult = { authorizationUrl?: string | null; reference?: string | null; alreadyHasTicket: boolean };

export type PaymentStatus = {
   reference: string;
   status: 'pending' | 'paid' | 'failed';
   sessionId: string;
   hasTicket: boolean;
   conferenceId?: string | null;
   failureReason?: string | null;
};

export type Ticket = {
   sessionId: string;
   conferenceId: string;
   title: string;
   category: string;
   startsAt?: string | null;
   creatorName: string;
   grantedAt: string;
   status: SessionStatus;
};

/** A session in the list of what is on */
export type DiscoverSession = {
   sessionId: string;
   title: string;
   description?: string | null;
   category: string;
   priceMinor: number;
   currency: string;
   startsAt?: string | null;
   creatorName: string;
   isHost: boolean;
   hasTicket: boolean;
};

const base = () => appSettings.conferenceUrl.replace(/\/+$/, '');
const auth = (accessToken?: string) => (accessToken ? { headers: { Authorization: `Bearer ${accessToken}` } } : {});

export async function fetchSession(sessionId: string, accessToken?: string): Promise<PublicSession> {
   return (await Axios.get<PublicSession>(`${base()}/v1/sessions/${encodeURIComponent(sessionId)}`, auth(accessToken)))
      .data;
}

export async function createSession(request: CreateSessionRequest): Promise<Session> {
   return (await Axios.post<Session>(`${base()}/v1/sessions`, request)).data;
}

export async function fetchMySessions(): Promise<Session[]> {
   return (await Axios.get<Session[]>(`${base()}/v1/sessions/mine`)).data;
}

export async function cancelSession(sessionId: string): Promise<void> {
   await Axios.post(`${base()}/v1/sessions/${encodeURIComponent(sessionId)}/cancel`);
}

export async function startCheckout(sessionId: string, email: string, accessToken: string): Promise<CheckoutResult> {
   return (
      await Axios.post<CheckoutResult>(
         `${base()}/v1/sessions/${encodeURIComponent(sessionId)}/checkout`,
         { email },
         auth(accessToken),
      )
   ).data;
}

export async function fetchPayment(reference: string, accessToken: string): Promise<PaymentStatus> {
   return (await Axios.get<PaymentStatus>(`${base()}/v1/payments/${encodeURIComponent(reference)}`, auth(accessToken)))
      .data;
}

export async function fetchMyTickets(): Promise<Ticket[]> {
   return (await Axios.get<Ticket[]>(`${base()}/v1/tickets/mine`)).data;
}

export const sessionLink = (sessionId: string) => new URL('/s/' + sessionId, document.baseURI).href;

/** The published sessions that are not over, the ones that start first at the top. */
export async function fetchDiscover(): Promise<DiscoverSession[]> {
   return (await Axios.get<DiscoverSession[]>(`${base()}/v1/sessions/discover`)).data;
}
