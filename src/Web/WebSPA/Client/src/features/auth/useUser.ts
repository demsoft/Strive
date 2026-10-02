import { User } from 'oidc-client-ts';
import { useAuth } from 'react-oidc-context';

/**
 * The signed in user. Only use inside of RequireAuth.
 */
export default function useUser(): User {
   const { user } = useAuth();
   if (!user) throw new Error('useUser() must only be used for authenticated users (inside of RequireAuth).');

   return user;
}
