import { createContext } from 'react';

/** Only populated from the server-validated redeemed HRM session. */
export const MeetingPortalContext = createContext<string | undefined>(undefined);
