import { Log, UserManagerSettings } from 'oidc-client-ts';
import { adaptToHost } from './app-host';

export type AppGitInfo = {
   commit: string;
   ref: string;
   timestamp: string;
};

export type AppSettings = {
   identityUrl: string;
   conferenceUrl: string;
   signalrHubUrl: string;
   equipmentSignalrHubUrl: string;
   frontendUrl: string;

   gitInfo: AppGitInfo;
};

// injected by ASP.Net Core, normalize urls. All urls don't have a trailing slash
const configured: AppSettings = Object.fromEntries(
   Object.entries((window as any).ENV).map(([name, value]) =>
      typeof value === 'string' ? [name, value.replace(/\/$/, '')] : [name, value],
   ),
) as AppSettings;

// another brand on its own host name: the app and the sign in pages are reached through the host the person came from
const appSettings: AppSettings = adaptToHost(configured, window.location.origin);

export default appSettings;

export const ocidConfig: UserManagerSettings = {
   client_id: 'spa',
   redirect_uri: `${appSettings.frontendUrl}/authentication/callback`,
   response_type: 'code',
   post_logout_redirect_uri: `${appSettings.frontendUrl}/`,
   scope: 'openid profile user.info',
   authority: appSettings.identityUrl,
   silent_redirect_uri: `${appSettings.frontendUrl}/authentication/silent_callback`,
   automaticSilentRenew: true,
   loadUserInfo: true,
};

Log.setLogger(console);
Log.setLevel(Log.ERROR);
