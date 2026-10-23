import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const api = vi.hoisted(() => ({
   fetchVideo: vi.fn(),
   videoLink: (id: string) => `https://gbedu.test/v/${id}`,
}));
const state = vi.hoisted(() => ({
   user: undefined as undefined | { access_token: string; profile?: Record<string, unknown> },
   isLoading: false,
   search: '',
   signinRedirect: vi.fn(),
}));

vi.mock('react-i18next', () => ({
   useTranslation: () => ({
      t: (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key),
   }),
}));
vi.mock('react-oidc-context', () => ({
   useAuth: () => ({ user: state.user, isLoading: state.isLoading, signinRedirect: state.signinRedirect }),
}));
vi.mock('src/services/api/sessions', () => ({ fetchPayment: (...args: unknown[]) => payments.fetchPayment(...args) }));
vi.mock('src/features/creator/errors', () => ({
   requestErrorMessage: (e: unknown) => String((e as Error).message ?? e),
}));
vi.mock('react-router-dom', () => ({
   Link: React.forwardRef<HTMLAnchorElement, { to: string; children: React.ReactNode; id?: string }>(
      ({ to, children, id }, ref) => (
         <a ref={ref} href={to} id={id}>
            {children}
         </a>
      ),
   ),
   useParams: () => ({ videoId: 'v1' }),
   useLocation: () => ({ pathname: '/v/v1', search: state.search }),
}));
vi.mock('src/components/BrandLogo', () => ({ default: () => null }));
vi.mock('./ReportVideoDialog', () => ({
   default: ({ open }: { open: boolean }) => (open ? <div id="report-dialog" /> : null),
}));
vi.mock('src/services/api/videos', () => api);

import WatchVideoPage from './WatchVideoPage';

const video = (over: Record<string, unknown> = {}) => ({
   videoId: 'v1',
   title: 'First reel',
   description: 'About piano',
   status: 'ready',
   priceMinor: 0,
   createdAt: '2026-10-20T10:00:00Z',
   isOwner: false,
   playbackUrl: 'https://r2.test/video.mp4?sig=1',
   posterUrl: 'https://r2.test/poster.jpg',
   creator: { creatorId: 'c1', displayName: 'Ada Lovelace', slug: 'ada' },
   ...over,
});

const payments = vi.hoisted(() => ({ fetchPayment: vi.fn() }));
const settle = () => act(async () => {});

let container: HTMLDivElement;
beforeEach(() => {
   container = document.createElement('div');
   document.body.appendChild(container);
   state.user = undefined;
   state.isLoading = false;
   state.search = '';
});
afterEach(() => {
   container.remove();
   document.body.innerHTML = '';
   vi.clearAllMocks();
});

const render = async () => {
   await act(async () => createRoot(container).render(<WatchVideoPage />));
   await settle();
};

test('the video plays, with its title, text and a link to the creator (by name when there is one)', async () => {
   api.fetchVideo.mockResolvedValue(video());
   await render();

   expect(api.fetchVideo).toHaveBeenCalledWith('v1', undefined);
   expect(document.querySelector('#video-player')?.getAttribute('src')).toBe('https://r2.test/video.mp4?sig=1');
   expect(document.querySelector('#watch-title')?.textContent).toBe('First reel');
   expect(document.querySelector('#watch-description')?.textContent).toBe('About piano');
   expect(document.querySelector('#watch-creator')?.getAttribute('href')).toBe('/u/ada');
});

test('a person who is signed in is asked for as themselves (the creator sees their own before it is ready)', async () => {
   state.user = { access_token: 'tok' };
   api.fetchVideo.mockResolvedValue(video());
   await render();

   expect(api.fetchVideo).toHaveBeenCalledWith('v1', 'tok');
});

test('nothing is asked for until it is known whether the person is signed in', async () => {
   state.isLoading = true;
   await render();

   expect(api.fetchVideo).not.toHaveBeenCalled();
});

test('a video that cannot be played yet says so instead of an empty player', async () => {
   api.fetchVideo.mockResolvedValue(video({ status: 'processing', playbackUrl: null }));
   await render();

   expect(document.querySelector('#video-player')).toBeNull();
   expect(document.querySelector('#watch-not-ready')).not.toBeNull();
});

test('an unknown video shows the not found message', async () => {
   api.fetchVideo.mockRejectedValue(new Error('404'));
   await render();

   expect(document.querySelector('#watch-not-found')).not.toBeNull();
   expect(document.querySelector('#video-player')).toBeNull();
});

test('the share button copies the link of the video', async () => {
   api.fetchVideo.mockResolvedValue(video());
   const writeText = vi.fn().mockResolvedValue(undefined);
   Object.assign(navigator, { clipboard: { writeText } });
   await render();

   await act(async () => (document.querySelector('#watch-share') as HTMLElement).click());

   expect(writeText).toHaveBeenCalledWith('https://gbedu.test/v/v1');
});

const locked = (over: Record<string, unknown> = {}) =>
   video({
      priceMinor: 300000,
      locked: true,
      playbackUrl: null,
      creator: { creatorId: 'c1', displayName: 'Ada Lovelace', slug: 'ada', passPriceMinor: 2000000, currency: 'NGN' },
      ...over,
   });

test('a video that costs money is not played: the lock, the price and the way to buy are shown', async () => {
   state.user = { access_token: 'tok', profile: { email: 'fan@example.com' } };
   api.fetchVideo.mockResolvedValue(locked());
   await render();

   expect(document.querySelector('#video-player')).toBeNull();
   expect(document.querySelector('#video-locked')).not.toBeNull();
   expect(document.querySelector('#video-pay')?.textContent).toContain('₦3,000');
   expect(document.querySelector('#video-pass-hint')?.textContent).toContain('₦20,000');
});

test('signed out: the buttons lead to the sign in and come back to this page', async () => {
   api.fetchVideo.mockResolvedValue(locked());
   await render();

   await act(async () => (document.querySelector('#video-sign-in') as HTMLElement).click());
   expect(state.signinRedirect).toHaveBeenCalledWith({ state: { url: '/v/v1' } });
   await act(async () => (document.querySelector('#video-sign-up') as HTMLElement).click());
   expect(state.signinRedirect).toHaveBeenLastCalledWith({ state: { url: '/v/v1' }, prompt: 'create' });
});

test('back from the payment page: when it was paid the video is loaded again and plays', async () => {
   state.user = { access_token: 'tok', profile: { email: 'fan@example.com' } };
   state.search = '?reference=ref1&trxref=ref1';
   api.fetchVideo.mockResolvedValueOnce(locked()).mockResolvedValue(video());
   payments.fetchPayment.mockResolvedValue({ status: 'paid', kind: 'video', videoId: 'v1' });
   await render();
   await settle();

   expect(payments.fetchPayment).toHaveBeenCalledWith('ref1', 'tok');
   expect(api.fetchVideo).toHaveBeenCalledTimes(2);
   expect(document.querySelector('#video-player')?.getAttribute('src')).toBe('https://r2.test/video.mp4?sig=1');
   expect(document.querySelector('#video-locked')).toBeNull();
});

test('back from the payment page: a failed payment is said clearly and the video stays locked', async () => {
   state.user = { access_token: 'tok', profile: { email: 'fan@example.com' } };
   state.search = '?reference=ref2';
   api.fetchVideo.mockResolvedValue(locked());
   payments.fetchPayment.mockResolvedValue({ status: 'failed', kind: 'video' });
   await render();
   await settle();

   expect(document.querySelector('#video-payment-failed')).not.toBeNull();
   expect(document.querySelector('#video-locked')).not.toBeNull();
});

test('a signed in person can report the video: the dialog opens', async () => {
   state.user = { access_token: 'tok', profile: {} };
   api.fetchVideo.mockResolvedValue(video());
   await render();

   expect(document.querySelector('#report-dialog')).toBeNull();
   await act(async () => (document.querySelector('#watch-report') as HTMLElement).click());

   expect(document.querySelector('#report-dialog')).not.toBeNull();
});

test('somebody who is signed out is asked to sign in first, and comes back to this video', async () => {
   api.fetchVideo.mockResolvedValue(video());
   await render();

   await act(async () => (document.querySelector('#watch-report') as HTMLElement).click());

   expect(state.signinRedirect).toHaveBeenCalledWith({ state: { url: '/v/v1' } });
   expect(document.querySelector('#report-dialog')).toBeNull();
});

test('the creator has no button to report their own video', async () => {
   state.user = { access_token: 'tok', profile: {} };
   api.fetchVideo.mockResolvedValue(video({ isOwner: true }));
   await render();

   expect(document.querySelector('#watch-report')).toBeNull();
});

test('it leads on to the reels, starting with this video', async () => {
   api.fetchVideo.mockResolvedValue(video());
   await render();

   expect(document.querySelector('#watch-more')?.getAttribute('href')).toBe('/reels/v1');
});
