import { useContext } from 'react';
import { AuthContext } from 'react-oidc-context';
import { useSelector } from 'react-redux';
import { selectMyParticipantId } from 'src/features/auth/selectors';

/**
 * The id of the current participant. Signed in users get it from their login, the recorder (which has no login, it
 * joins with a token) from the participant id the recording route stored.
 */
export default function useMyParticipantId(): string {
   const auth = useContext(AuthContext);
   const storedId = useSelector(selectMyParticipantId);

   const id = auth?.user?.profile.sub ?? storedId;
   if (!id) throw new Error('useMyParticipantId() needs a signed in user or a participant id in the state.');

   return id;
}
