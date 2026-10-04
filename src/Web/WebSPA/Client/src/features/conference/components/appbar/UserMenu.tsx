import {
   Avatar,
   Box,
   Divider,
   IconButton,
   ListItemIcon,
   ListItemText,
   Menu,
   MenuItem,
   Tooltip,
   Typography,
} from '@mui/material';
import LockOpenOutlinedIcon from '@mui/icons-material/LockOpenOutlined';
import MonitorHeartOutlinedIcon from '@mui/icons-material/MonitorHeartOutlined';
import LogoutIcon from '@mui/icons-material/Logout';
import PowerSettingsNewIcon from '@mui/icons-material/PowerSettingsNew';
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined';
import TuneIcon from '@mui/icons-material/Tune';
import VideoLibraryOutlinedIcon from '@mui/icons-material/VideoLibraryOutlined';
import React, { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from 'react-oidc-context';
import { Link as RouterLink } from 'react-router-dom';
import useUser from 'src/features/auth/useUser';
import { brand } from 'src/theme';

type Props = {
   /** Settings are an own button in the app bar, on small screens they are part of this menu. */
   showSettings: boolean;
   canManageRecordings: boolean;
   canCloseConference: boolean;
   onShowPermissions: () => void;
   onShowRecordings: () => void;
   onChangeConference: () => void;
   onCloseConference: () => void;
   onOpenSettings: () => void;
};

const initials = (name?: string) =>
   (name ?? '?')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((x) => Array.from(x)[0]?.toUpperCase())
      .join('') || '?';

/** Who is signed in, and everything that is not needed all the time. */
export default function UserMenu(props: Props) {
   const { t } = useTranslation();
   const auth = useAuth();
   const user = useUser();

   const buttonRef = useRef<HTMLButtonElement>(null);
   const [open, setOpen] = useState(false);

   const close = () => setOpen(false);
   const run = (action: () => void) => () => {
      close();
      action();
   };

   const name = user.profile.name;
   const roles = ([] as unknown[]).concat(user.profile.role ?? []);
   const isServerAdmin = roles.includes('serveradmin');

   return (
      <>
         <Tooltip title={name ?? ''}>
            <IconButton
               id="user-menu-button"
               ref={buttonRef}
               aria-label={t('conference.appbar.account_menu')}
               aria-haspopup="menu"
               onClick={() => setOpen(true)}
               sx={{ ml: 0.5, p: 0.5 }}
            >
               <Avatar sx={{ width: 32, height: 32, fontSize: 13, fontWeight: 700, background: brand.gradient }}>
                  {initials(name)}
               </Avatar>
            </IconButton>
         </Tooltip>
         <Menu
            open={open}
            onClose={close}
            anchorEl={buttonRef.current}
            anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
            transformOrigin={{ vertical: 'top', horizontal: 'right' }}
            slotProps={{ paper: { sx: { minWidth: 248, mt: 1 } } }}
         >
            <Box sx={{ px: 2, pt: 1, pb: 1.5 }}>
               <Typography variant="caption" color="text.secondary">
                  {t('conference.appbar.signed_in_as')}
               </Typography>
               <Typography variant="subtitle1" sx={{ fontWeight: 700, lineHeight: 1.3 }} noWrap>
                  {name}
               </Typography>
            </Box>
            <Divider />
            {props.showSettings && (
               <MenuItem onClick={run(props.onOpenSettings)}>
                  <ListItemIcon>
                     <SettingsOutlinedIcon fontSize="small" />
                  </ListItemIcon>
                  <ListItemText>{t('common:settings')}</ListItemText>
               </MenuItem>
            )}
            <MenuItem onClick={run(props.onShowPermissions)}>
               <ListItemIcon>
                  <LockOpenOutlinedIcon fontSize="small" />
               </ListItemIcon>
               <ListItemText>{t('conference.appbar.show_my_permissions')}</ListItemText>
            </MenuItem>
            {props.canManageRecordings && (
               <MenuItem onClick={run(props.onShowRecordings)}>
                  <ListItemIcon>
                     <VideoLibraryOutlinedIcon fontSize="small" />
                  </ListItemIcon>
                  <ListItemText>{t('conference.recording.list_title')}</ListItemText>
               </MenuItem>
            )}
            <MenuItem onClick={run(props.onChangeConference)}>
               <ListItemIcon>
                  <TuneIcon fontSize="small" />
               </ListItemIcon>
               <ListItemText>{t('conference.appbar.change_conference_settings')}</ListItemText>
            </MenuItem>
            {props.canCloseConference && (
               <MenuItem onClick={run(props.onCloseConference)} sx={{ color: 'error.main' }}>
                  <ListItemIcon sx={{ color: 'inherit' }}>
                     <PowerSettingsNewIcon fontSize="small" />
                  </ListItemIcon>
                  <ListItemText>{t('conference.appbar.close_conference')}</ListItemText>
               </MenuItem>
            )}
            {isServerAdmin && (
               <MenuItem component={RouterLink} to="/admin" onClick={close} id="admin-overview-link">
                  <ListItemIcon>
                     <MonitorHeartOutlinedIcon fontSize="small" />
                  </ListItemIcon>
                  <ListItemText>{t('admin.menu')}</ListItemText>
               </MenuItem>
            )}
            <Divider />
            <MenuItem onClick={run(() => auth.signoutRedirect())}>
               <ListItemIcon>
                  <LogoutIcon fontSize="small" />
               </ListItemIcon>
               <ListItemText>{t('common:sign_out')}</ListItemText>
            </MenuItem>
         </Menu>
      </>
   );
}
