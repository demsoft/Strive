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
import * as api from 'src/services/api/videos';
import { REPORT_REASONS, ReportReason } from '../reportReasons';

type Props = { videoId: string; open: boolean; onClose: () => void };

/** A person says what is wrong with a video. After it was sent the dialog says thanks. */
export default function ReportVideoDialog({ videoId, open, onClose }: Props) {
   const { t } = useTranslation();
   const [reason, setReason] = useState<ReportReason | ''>('');
   const [details, setDetails] = useState('');
   const [sending, setSending] = useState(false);
   const [sent, setSent] = useState(false);
   const [error, setError] = useState<string | null>(null);

   const send = async () => {
      if (!reason) return;
      setSending(true);
      setError(null);
      try {
         await api.reportVideo(videoId, reason, details.trim());
         setSent(true);
      } catch (e) {
         setError(requestErrorMessage(e));
      }
      setSending(false);
   };

   return (
      <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
         <DialogTitle>{t('videos.report_title')}</DialogTitle>
         <DialogContent>
            {sent ? (
               <>
                  <Alert severity="success" id="report-sent">
                     {t('videos.report_thanks')}
                  </Alert>
                  <Button fullWidth sx={{ mt: 2 }} onClick={onClose} id="report-close">
                     {t('videos.report_close')}
                  </Button>
               </>
            ) : (
               <>
                  <Typography color="text.secondary" sx={{ mb: 2 }}>
                     {t('videos.report_text')}
                  </Typography>
                  <TextField
                     id="report-reason"
                     select
                     fullWidth
                     label={t('videos.report_reason')}
                     value={reason}
                     onChange={(e) => setReason(e.target.value as ReportReason)}
                  >
                     {REPORT_REASONS.map((r) => (
                        <MenuItem key={r} value={r}>
                           {t(`videos.reason_${r}`)}
                        </MenuItem>
                     ))}
                  </TextField>
                  <TextField
                     id="report-details"
                     fullWidth
                     multiline
                     minRows={2}
                     label={t('videos.report_details')}
                     value={details}
                     onChange={(e) => setDetails(e.target.value.slice(0, 500))}
                     sx={{ mt: 2 }}
                  />
                  {error && (
                     <Alert severity="error" sx={{ mt: 2 }} id="report-error">
                        {error}
                     </Alert>
                  )}
                  <Button
                     id="report-send"
                     variant="contained"
                     fullWidth
                     disabled={!reason || sending}
                     onClick={send}
                     sx={{ mt: 2 }}
                  >
                     {sending ? <CircularProgress size={22} color="inherit" /> : t('videos.report_send')}
                  </Button>
               </>
            )}
         </DialogContent>
      </Dialog>
   );
}
