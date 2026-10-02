import { useEffect } from 'react';
import { useHistory } from 'react-router-dom';
import { SigninState } from 'src/features/auth/components/StriveAuthProvider';
import useUser from 'src/features/auth/useUser';

export default function RedirectToConference() {
   const history = useHistory();
   const oidcUser = useUser();

   const redirectUrl = (oidcUser.state as SigninState | undefined)?.url;

   useEffect(() => {
      if (redirectUrl && history.location.pathname !== redirectUrl) {
         history.replace(redirectUrl);
      }
   }, [redirectUrl]);

   return null;
}
