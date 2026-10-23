import Axios from 'axios';
import appSettings from 'src/config';
import type { Video } from './videos';

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
   /** how long it is meant to last */
   durationMinutes: number;
   createdAt: string;
   /** after a cancellation: tickets given back, and tickets whose refund did not work yet */
   refunded: number;
   refundsPending: number;
};

export type PublicCreator = {
   creatorId: string;
   displayName: string;
   bio?: string | null;
   /** what the 30-day pass of this creator costs; none if the creator does not sell one */
   passPriceMinor?: number | null;
   currency?: string | null;
   /** the short name of the page: /u/ada */
   slug?: string | null;
   /** changes when the photo does; none: no photo */
   photoVersion?: string | null;
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
   creator: PublicCreator;
   isHost: boolean;
   hasTicket: boolean;
   /** only for the host and people with a ticket */
   conferenceId?: string | null;
   durationMinutes: number;
   /** the host has the room open right now */
   isLive: boolean;
};

export type CreateSessionRequest = {
   title: string;
   description?: string;
   category: string;
   priceMinor: number;
   startsAt?: string;
   durationMinutes?: number;
};

export type CheckoutResult = { authorizationUrl?: string | null; reference?: string | null; alreadyHasTicket: boolean };

export type PaymentStatus = {
   reference: string;
   status: 'pending' | 'paid' | 'failed';
   sessionId: string;
   hasTicket: boolean;
   conferenceId?: string | null;
   failureReason?: string | null;
   kind?: 'ticket' | 'pass' | 'video';
   videoId?: string | null;
   creatorId?: string | null;
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
   /** the price was given back because the session was called off */
   refunded: boolean;
   durationMinutes: number;
   /** the host has the room open right now */
   isLive: boolean;
   creatorId?: string | null;
   creatorPhotoVersion?: string | null;
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
   durationMinutes: number;
   /** the host has the room open right now (not just: the start time has passed) */
   isLive: boolean;
   creatorId?: string | null;
   creatorPhotoVersion?: string | null;
};

export type CreatorPage = {
   creator: PublicCreator;
   sessions: DiscoverSession[];
   /** the person has a running pass of this creator */
   hasPass: boolean;
   passValidUntil?: string | null;
   /** the server renews passes by itself: the person may choose it */
   autoRenewAvailable?: boolean;
   /** the pass of the person renews on this day */
   passRenewsAt?: string | null;
   /** the ready videos of the creator, newest first */
   videos?: Video[];
};

export type Pass = {
   creatorId: string;
   creatorName: string;
   validUntil: string;
   /** set if the pass renews itself */
   subscriptionId?: string | null;
   renewsAt?: string | null;
   creatorSlug?: string | null;
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

/** What a creator has on, for their page. The token (if the person is signed in) marks their own and ticketed sessions. */
export async function fetchCreatorPage(creatorId: string, accessToken?: string): Promise<CreatorPage> {
   return (
      await Axios.get<CreatorPage>(`${base()}/v1/creators/${encodeURIComponent(creatorId)}/page`, auth(accessToken))
   ).data;
}

/** The address of the page of a creator: the short name if there is one, else the id. */
export const creatorPageLink = (creatorIdOrSlug: string) => new URL('/u/' + creatorIdOrSlug, document.baseURI).href;

/** What comes after /u/ in the link of a creator. */
export const creatorPath = (creator: { creatorId: string; slug?: string | null }) => creator.slug || creator.creatorId;

export async function startPassCheckout(
   creatorId: string,
   email: string,
   accessToken: string,
   autoRenew = false,
): Promise<CheckoutResult> {
   return (
      await Axios.post<CheckoutResult>(
         `${base()}/v1/creators/${encodeURIComponent(creatorId)}/pass/checkout`,
         { email, autoRenew },
         auth(accessToken),
      )
   ).data;
}

/** The passes of the person that are running. */
export async function fetchMyPasses(): Promise<Pass[]> {
   return (await Axios.get<Pass[]>(`${base()}/v1/passes/mine`)).data;
}

/** Stops a pass from renewing. The pass that was paid for runs until its end. */
export async function cancelPassSubscription(subscriptionId: string): Promise<void> {
   await Axios.post(`${base()}/v1/passes/subscriptions/${encodeURIComponent(subscriptionId)}/cancel`);
}
