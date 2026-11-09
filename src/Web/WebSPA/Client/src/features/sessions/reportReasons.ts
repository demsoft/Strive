import type { SessionReportReason } from 'src/services/api/sessions';

/** What a person can say is wrong with a session; the first is for the owner of the rights to what is shown. */
export const SESSION_REPORT_REASONS: SessionReportReason[] = [
   'rightsHolder',
   'illegal',
   'misleading',
   'abuse',
   'spam',
   'other',
];
