import { Grid, IconButton, Typography } from '@mui/material';

import { makeStyles } from 'tss-react/mui';

const useStyles = makeStyles()({
   container: {
      width: 200,
   },
   emojiIcon: {
      fontSize: 18,
   },
});

type Props = {
   onEmojiSelected: (s: string) => void;
   onClose: () => void;
};

const emojis: string[] = ['👍', '👎', '🎉', '😂', '😭', '❤️', '🔥', '🤔', '😫', '🙄', '🚀', '👀'];

export default function EmojisPopper({ onEmojiSelected }: Props) {
   const { classes } = useStyles();
   return (
      <Grid container className={classes.container}>
         {emojis.map((x) => (
            <Grid key={x} size={3}>
               <IconButton onClick={() => onEmojiSelected(x)} aria-label={`Insert emoji ${x}`} size="large">
                  <Typography className={classes.emojiIcon}>{x}</Typography>
               </IconButton>
            </Grid>
         ))}
      </Grid>
   );
}
