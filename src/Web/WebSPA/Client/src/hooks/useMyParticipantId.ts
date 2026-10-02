import useUser from 'src/features/auth/useUser';

export default function useMyParticipantId() {
   const oidcUser = useUser();
   return oidcUser.profile.sub;
}
