import { useCallback, useEffect, useState } from 'react';
import * as api from 'src/services/api/videos';

type Start = { likes?: number; liked?: boolean };

/**
 * The likes of one video as the person sees them. Pressing the heart changes it at once; the server's answer then
 * sets the real number, and a refusal puts it back. Signed out, the person is asked to sign in instead.
 */
export default function useVideoLike(videoId: string, start: Start, signedIn: boolean, onSignIn: () => void) {
   const [state, setState] = useState({ likes: start.likes ?? 0, liked: start.liked ?? false });

   // another video, or the same one loaded again with news
   useEffect(() => {
      setState({ likes: start.likes ?? 0, liked: start.liked ?? false });
   }, [videoId, start.likes, start.liked]);

   const toggle = useCallback(async () => {
      if (!signedIn) return onSignIn();

      const before = state;
      setState({ liked: !before.liked, likes: Math.max(0, before.likes + (before.liked ? -1 : 1)) });
      try {
         setState(await (before.liked ? api.unlikeVideo(videoId) : api.likeVideo(videoId)));
      } catch {
         setState(before);
      }
   }, [signedIn, onSignIn, state, videoId]);

   return { ...state, toggle };
}
