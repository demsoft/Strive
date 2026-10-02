import { Box, Grid, IconButton, Typography } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import { FormatSize } from '@mui/icons-material';
import React, { useRef, useState } from 'react';
import ToolIcon from './ToolIcon';
import ToolPopper from './ToolPopper';

const availableFontSizes = [12, 18, 24, 30, 36];

const useStyles = makeStyles()((theme) => ({
   strokeButton: {
      height: '100%',
      width: 64,
   },
   strokeButtonSelected: {
      backgroundColor: theme.palette.grey[800],
   },
}));

type Props = {
   value: number;
   onChange: (value: number) => void;
};

export default function FontSizeTool({ value, onChange }: Props) {
   const { classes, cx } = useStyles();

   const [open, setOpen] = useState(false);
   const anchorEl = useRef(null);

   const handleClose = () => setOpen(false);
   const handleOpen = () => setOpen(true);

   const handleChange = (width: number) => () => {
      onChange(width);
      handleClose();
   };

   return (
      <>
         <ToolIcon icon={<FormatSize fontSize="small" />} ref={anchorEl} onClick={handleOpen} />

         <ToolPopper open={open} anchorEl={anchorEl.current} onClose={handleClose}>
            <Box
               sx={{
                  p: 1,
               }}
            >
               <Grid container>
                  {availableFontSizes.map((size) => (
                     <Grid key={size}>
                        <IconButton
                           onClick={handleChange(size)}
                           className={cx(classes.strokeButton, value === size && classes.strokeButtonSelected)}
                           title={`${size}px`}
                           size="large"
                        >
                           <Typography style={{ fontSize: size }}>A</Typography>
                        </IconButton>
                     </Grid>
                  ))}
               </Grid>
            </Box>
         </ToolPopper>
      </>
   );
}
