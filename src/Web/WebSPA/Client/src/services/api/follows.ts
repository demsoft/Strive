import Axios from 'axios';
import appSettings from 'src/config';
import type { PublicCreator } from './sessions';

const base = () => `${appSettings.conferenceUrl.replace(/\/+$/, '')}/v1/creators`;

/** The person follows the creator. Doing it again changes nothing. */
export async function followCreator(creatorId: string): Promise<void> {
   await Axios.put(`${base()}/${encodeURIComponent(creatorId)}/follow`);
}

export async function unfollowCreator(creatorId: string): Promise<void> {
   await Axios.delete(`${base()}/${encodeURIComponent(creatorId)}/follow`);
}

/** The creators the person follows, the latest follow first. */
export async function fetchFollowing(): Promise<PublicCreator[]> {
   return (await Axios.get<PublicCreator[]>(`${base()}/following`)).data;
}

export type NoticeState = 'off' | 'pending' | 'on';

/**
 * The follower asks for mail when the creator posts. A mail with a link goes to the address first: nothing is sent
 * before its owner confirmed it.
 */
export async function startEmailNotices(creatorId: string, email: string): Promise<void> {
   await Axios.put(`${base()}/${encodeURIComponent(creatorId)}/follow/email`, { email });
}

export async function stopEmailNotices(creatorId: string): Promise<void> {
   await Axios.delete(`${base()}/${encodeURIComponent(creatorId)}/follow/email`);
}

export type NoticeAnswer = { creatorName: string; status: 'on' | 'off' };

const notices = () => `${appSettings.conferenceUrl.replace(/\/+$/, '')}/v1/notices`;

/** The link in the confirmation mail. */
export async function confirmNotice(token: string): Promise<NoticeAnswer> {
   return (await Axios.post<NoticeAnswer>(`${notices()}/${encodeURIComponent(token)}/confirm`)).data;
}

/** The link at the bottom of every mail. */
export async function stopNotice(token: string): Promise<NoticeAnswer> {
   return (await Axios.post<NoticeAnswer>(`${notices()}/${encodeURIComponent(token)}/stop`)).data;
}
