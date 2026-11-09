import { Alert, Box, CircularProgress } from '@mui/material';
import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { requestErrorMessage } from 'src/features/creator/errors';
import { CreatorProfile, fetchMyCreatorProfile } from 'src/services/api/creators';
import { Session } from 'src/services/api/sessions';
import NewMatchForm from './NewMatchForm';
import OrganizerRequest from './OrganizerRequest';

type Props = { onCreated: (session: Session) => void };

/** Where an organizer sets up a match: the form once an administrator approved the organizer, the request before. */
export default function MatchSetup({ onCreated }: Props) {
   const { t } = useTranslation();
   const [profile, setProfile] = useState<CreatorProfile | null | undefined>(undefined);
   const [error, setError] = useState<string | null>(null);

   useEffect(() => {
      fetchMyCreatorProfile().then(setProfile, (e) => setError(requestErrorMessage(e)));
   }, []);

   if (error)
      return (
         <Alert severity="error" id="match-setup-error">
            {error}
         </Alert>
      );
   if (profile === undefined)
      return (
         <Box sx={{ display: 'flex', justifyContent: 'center', p: 3 }}>
            <CircularProgress />
         </Box>
      );
   if (profile === null)
      return (
         <Alert severity="info" id="match-setup-no-profile">
            {t('matches.needs_profile')}
         </Alert>
      );
   if (profile.organizerStatus !== 'approved') return <OrganizerRequest profile={profile} onChanged={setProfile} />;

   return <NewMatchForm onCreated={onCreated} />;
}
