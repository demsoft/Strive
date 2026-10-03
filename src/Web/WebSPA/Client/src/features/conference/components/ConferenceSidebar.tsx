import { alpha, IconButton, Paper, Tooltip } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import ViewSidebarOutlinedIcon from '@mui/icons-material/ViewSidebarOutlined';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useDispatch, useSelector } from 'react-redux';
import RoomsList from 'src/features/rooms/components/RoomsList';
import SceneManagement from 'src/features/scenes/components/SceneManagement';
import { RootState } from 'src/store';
import { setSidebarOpen } from '../reducer';

const drawerWidth = 216;

const useStyles = makeStyles()((theme) => ({
   drawerContainer: {
      padding: theme.spacing(1, 0, 1, 1),
      width: drawerWidth,
      height: '100%',
   },
   drawer: {
      display: 'flex',
      flexDirection: 'column',

      flexShrink: 0,
      width: '100%',
      whiteSpace: 'nowrap',

      backgroundColor: alpha(theme.palette.background.paper, 0.5),
      height: '100%',

      borderColor: theme.palette.divider,
      borderWidth: 0,
   },
   listRoot: {
      position: 'relative',
      height: '100%',
   },
   toggleButton: {
      position: 'absolute',
      top: 8,
      right: -38,
      zIndex: theme.zIndex.drawer,
      display: 'flex',
      flexDirection: 'column',
   },
   toggleIconButton: {
      border: `1px solid ${theme.palette.divider}`,
      backgroundColor: alpha(theme.palette.background.paper, 0.7),
      borderRadius: 10,
      '&:hover': { backgroundColor: theme.palette.background.paper },
   },
}));

export default function ConferenceSidebar() {
   const { classes } = useStyles();
   const { t } = useTranslation();

   const dispatch = useDispatch();
   const open = useSelector((state: RootState) => state.conference.sidebarOpen);
   const setOpen = (visible: boolean) => dispatch(setSidebarOpen(visible));

   const handleToggle = () => setOpen(!open);

   return (
      <div className={classes.listRoot}>
         {open && (
            <div className={classes.drawerContainer}>
               <Paper className={classes.drawer} elevation={1}>
                  <RoomsList />
                  <SceneManagement />
               </Paper>
            </div>
         )}
         <div className={classes.toggleButton}>
            <Tooltip title={open ? t('conference.sidebar.hide') : t('conference.sidebar.show')} placement="right">
               <IconButton
                  id="toggle-sidebar"
                  aria-label={open ? t('conference.sidebar.hide') : t('conference.sidebar.show')}
                  aria-pressed={open}
                  size="small"
                  className={classes.toggleIconButton}
                  color={open ? 'primary' : 'default'}
                  onClick={handleToggle}
               >
                  <ViewSidebarOutlinedIcon fontSize="small" />
               </IconButton>
            </Tooltip>
         </div>
      </div>
   );
}
