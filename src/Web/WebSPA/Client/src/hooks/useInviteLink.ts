import { useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch } from 'react-redux';
import { useParams } from 'react-router-dom';
import { showMessage } from 'src/store/notifier/actions';
import { ConferenceRouteParams } from 'src/routes/types';

/** The link participants can use to join this conference and a function that copies it to the clipboard. */
export default function useInviteLink() {
   const { id: conferenceId } = useParams<ConferenceRouteParams>();
   const dispatch = useDispatch();
   const { t } = useTranslation();

   const link = conferenceId ? new URL('/c/' + conferenceId, document.baseURI).href : '';

   const copy = useCallback(async () => {
      try {
         await navigator.clipboard.writeText(link);
         dispatch(showMessage({ type: 'success', message: t('conference.invite.copied') }));
      } catch {
         // the clipboard is not available (e.g. insecure context), show the link so it can be copied manually
         dispatch(showMessage({ type: 'info', message: link, icon: '🔗' }));
      }
   }, [link, dispatch, t]);

   return { link, copy };
}
