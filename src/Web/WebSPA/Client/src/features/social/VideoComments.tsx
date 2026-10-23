import { Alert, Box, Button, CircularProgress, IconButton, TextField, Typography } from '@mui/material';
import DeleteOutlinedIcon from '@mui/icons-material/DeleteOutlined';
import { DateTime } from 'luxon';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { requestErrorMessage } from 'src/features/creator/errors';
import * as api from 'src/services/api/videos';
import { VideoComment } from 'src/services/api/videos';
import { brand } from 'src/theme';
import { MAX_COMMENT_LENGTH } from './commentRules';

type Props = {
   videoId: string;
   signedIn: boolean;
   onSignIn: () => void;
   /** the video costs money and the person has not paid: they can read, not write */
   locked?: boolean;
   /** told whenever the number of comments is known or changes */
   onTotal?: (total: number) => void;
};

/** The comments under a video: the newest first, a box to write one, and a way to take away what may be taken away. */
export default function VideoComments({ videoId, signedIn, onSignIn, locked, onTotal }: Props) {
   const { t } = useTranslation();
   const [items, setItems] = useState<VideoComment[] | null>(null);
   const [total, setTotal] = useState(0);
   const [next, setNext] = useState<string | null>(null);
   const [failed, setFailed] = useState(false);
   const [loadingMore, setLoadingMore] = useState(false);
   const [text, setText] = useState('');
   const [sending, setSending] = useState(false);
   const [error, setError] = useState<string | null>(null);

   const report = useCallback(
      (value: number) => {
         setTotal(value);
         onTotal?.(value);
      },
      [onTotal],
   );

   // another video, or the person signed in (what may be deleted depends on who asks)
   useEffect(() => {
      let cancelled = false;
      setItems(null);
      setFailed(false);
      api.fetchComments(videoId).then(
         (list) => {
            if (cancelled) return;
            setItems(list.items);
            setNext(list.next ?? null);
            report(list.total);
         },
         () => !cancelled && setFailed(true),
      );
      return () => {
         cancelled = true;
      };
      // eslint-disable-next-line react-hooks/exhaustive-deps
   }, [videoId, signedIn]);

   const older = async () => {
      if (!next) return;
      setLoadingMore(true);
      try {
         const list = await api.fetchComments(videoId, next);
         setItems((current) => {
            const known = new Set((current ?? []).map((x) => x.commentId));
            return [...(current ?? []), ...list.items.filter((x) => !known.has(x.commentId))];
         });
         setNext(list.next ?? null);
      } catch {
         setFailed(true);
      }
      setLoadingMore(false);
   };

   const send = async () => {
      const body = text.trim();
      if (!body || sending) return;
      setSending(true);
      setError(null);
      try {
         const comment = await api.postComment(videoId, body);
         setItems((current) => [comment, ...(current ?? [])]);
         report(total + 1);
         setText('');
      } catch (e) {
         setError(requestErrorMessage(e));
      }
      setSending(false);
   };

   const remove = async (comment: VideoComment) => {
      try {
         await api.deleteComment(videoId, comment.commentId);
         setItems((current) => (current ?? []).filter((x) => x.commentId !== comment.commentId));
         report(Math.max(0, total - 1));
      } catch (e) {
         setError(requestErrorMessage(e));
      }
   };

   return (
      <Box id="video-comments">
         <Typography variant="h6" sx={{ fontWeight: 800, mb: 1 }}>
            {t('social.comments_title')} {total > 0 && <span className="comments-total">{total}</span>}
         </Typography>

         {locked ? (
            <Alert severity="info" id="comments-locked" sx={{ mb: 2 }}>
               {t('social.comment_locked')}
            </Alert>
         ) : signedIn ? (
            <Box sx={{ display: 'flex', gap: 1, alignItems: 'flex-start', mb: 2 }}>
               <TextField
                  id="comment-text"
                  fullWidth
                  multiline
                  maxRows={4}
                  size="small"
                  value={text}
                  placeholder={t('social.comment_placeholder')}
                  slotProps={{ htmlInput: { maxLength: MAX_COMMENT_LENGTH } }}
                  onChange={(e) => setText(e.target.value)}
               />
               <Button
                  id="comment-send"
                  variant="contained"
                  disabled={sending || !text.trim()}
                  onClick={send}
                  sx={{ background: brand.gradient, flexShrink: 0 }}
               >
                  {sending ? <CircularProgress size={20} color="inherit" /> : t('social.comment_send')}
               </Button>
            </Box>
         ) : (
            <Button id="comment-sign-in" variant="outlined" onClick={onSignIn} sx={{ mb: 2 }}>
               {t('social.comment_sign_in')}
            </Button>
         )}

         {error && (
            <Alert severity="error" id="comments-error" sx={{ mb: 2 }} onClose={() => setError(null)}>
               {error}
            </Alert>
         )}
         {failed && (
            <Alert severity="error" id="comments-failed" sx={{ mb: 2 }}>
               {t('social.comments_failed')}
            </Alert>
         )}
         {items === null && !failed && (
            <Box sx={{ display: 'flex', justifyContent: 'center', p: 2 }}>
               <CircularProgress size={24} />
            </Box>
         )}
         {items?.length === 0 && (
            <Typography color="text.secondary" id="comments-none">
               {t('social.comments_none')}
            </Typography>
         )}

         {items?.map((comment) => (
            <Box key={comment.commentId} className="comment" sx={{ display: 'flex', gap: 1, py: 1 }}>
               <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Typography variant="body2" sx={{ fontWeight: 700 }}>
                     <span className="comment-author">{comment.authorName}</span>{' '}
                     <Typography component="span" variant="caption" color="text.secondary">
                        {DateTime.fromISO(comment.createdAt).toRelative()}
                     </Typography>
                  </Typography>
                  <Typography className="comment-text" sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
                     {comment.text}
                  </Typography>
               </Box>
               {comment.canDelete && (
                  <IconButton
                     className="comment-delete"
                     size="small"
                     aria-label={t('social.comment_delete')}
                     onClick={() => remove(comment)}
                  >
                     <DeleteOutlinedIcon fontSize="small" />
                  </IconButton>
               )}
            </Box>
         ))}

         {next && (
            <Box sx={{ textAlign: 'center', mt: 1 }}>
               <Button id="comments-older" size="small" disabled={loadingMore} onClick={older}>
                  {loadingMore ? <CircularProgress size={18} color="inherit" /> : t('social.comments_older')}
               </Button>
            </Box>
         )}
      </Box>
   );
}
