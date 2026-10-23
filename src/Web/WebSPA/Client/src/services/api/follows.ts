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
