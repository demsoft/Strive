import {
   Alert,
   Button,
   CircularProgress,
   Dialog,
   DialogContent,
   DialogTitle,
   MenuItem,
   TextField,
   Typography,
} from '@mui/material';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { requestErrorMessage } from 'src/features/creator/errors';
import * as api from 'src/services/api/sessions';
import { SessionReportReason } from 'src/services/api/sessions';
import { SESSION_REPORT_REASONS } from '../reportReasons';

type Props = { sessionId: string; open: boolean; onClose: () => void };

/** A person says what is wrong with a session, for example a match shown without the right to. */
export default function ReportSessionDialog({ sessionId, open, onClose }: Props) {
   const { t } = useTranslation();
   const [reason, setReason] = useState<SessionReportReason | ''>('');
   const [details, setDetails] = useState('');
   const [sending, setSending] = useState(false);
   const [sent, setSent] = useState(false);
   const [error, setError] = useState<string | null>(null);

   const send = async () => {
      if (!reason) return;
      setSending(true);
      setError(null);
      try {
         await api.reportSession(sessionId, reason, details.trim());
         setSent(true);
      } catch (e) {
         setError(requestErrorMessage(e));
      }
      setSending(false);
   };

   return (
      <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
         <DialogTitle>{t('matches.report_title')}</DialogTitle>
         <DialogContent>
            {sent ? (
               <>
                  <Alert severity="success" id="session-report-sent">
                     {t('matches.report_thanks')}
                  </Alert>
                  <Button fullWidth sx={{ mt: 2 }} onClick={onClose} id="session-report-close">
                     {t('matches.report_close')}
                  </Button>
               </>
            ) : (
               <>
                  <Typography color="text.secondary" sx={{ mb: 2 }}>
                     {t('matches.report_text')}
                  </Typography>
                  <TextField
                     id="session-report-reason"
                     select
                     fullWidth
                     label={t('matches.report_reason')}
                     value={reason}
                     onChange={(e) => setReason(e.target.value as SessionReportReason)}
                  >
                     {SESSION_REPORT_REASONS.map((r) => (
                        <MenuItem key={r} value={r}>
                           {t(`matches.reason_${r}`)}
                        </MenuItem>
                     ))}
                  </TextField>
                  <TextField
                     id="session-report-details"
                     fullWidth
                     multiline
                     minRows={2}
                     label={t('matches.report_details')}
                     value={details}
                     onChange={(e) => setDetails(e.target.value.slice(0, 500))}
                     sx={{ mt: 2 }}
                  />
                  {error && (
                     <Alert severity="error" sx={{ mt: 2 }} id="session-report-error">
                        {error}
                     </Alert>
                  )}
                  <Button
                     id="session-report-send"
                     variant="contained"
                     fullWidth
                     disabled={!reason || sending}
                     onClick={send}
                     sx={{ mt: 2 }}
                  >
                     {sending ? <CircularProgress size={22} color="inherit" /> : t('matches.report_send')}
                  </Button>
               </>
            )}
         </DialogContent>
      </Dialog>
   );
}
