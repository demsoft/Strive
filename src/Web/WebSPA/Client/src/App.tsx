import { AuthenticationProvider, oidcLog, OidcSecure } from '@axa-fr/react-oidc-context';
import { createTheme, CssBaseline, responsiveFontSizes, ThemeProvider } from '@mui/material';
import { blue, pink } from '@mui/material/colors';
import { makeStyles } from 'tss-react/mui';
import { Toaster } from 'react-hot-toast';
import { BrowserRouter, Route, Switch } from 'react-router-dom';
import { ocidConfig } from 'src/config';
import AuthCallback from 'src/features/auth/components/AuthCallback';
import NotAuthenticated from 'src/features/auth/components/NotAuthenticated';
import AuthenticatingComponent from './features/auth/components/AuthenticatingComponent';
import SessionLostComponent from './features/auth/components/SessionLostComponent';
import UserInteractionListener from './features/media/components/UserInteractionListener';
import RedirectToConference from './RedirectToConference';
import AuthenticatedRoutes from './routes/AuthenticatedRoutes';
import EquipmentRoute from './routes/EquipmentRoute';

const useStyles = makeStyles()((theme) => ({
   toast: {
      backgroundColor: theme.palette.background.paper,
      color: theme.palette.text.primary,
   },
}));

const theme = responsiveFontSizes(
   createTheme({
      palette: {
         mode: 'dark',
         primary: {
            main: blue[500],
         },
         secondary: {
            main: pink[500],
         },
         background: {
            default: 'rgb(20, 20, 22)',
            paper: '#303030',
         },
      },
      components: {
         // keep the MUI v4 default (standard) instead of the outlined variant introduced in v5
         MuiTextField: { defaultProps: { variant: 'standard' } },
         MuiSelect: { defaultProps: { variant: 'standard' } },
         MuiFormControl: { defaultProps: { variant: 'standard' } },
      },
   }),
);

function App() {
   return (
      <ThemeProvider theme={theme}>
         <MaterialUiToaster />
         <UserInteractionListener />
         <CssBaseline />
         <BrowserRouter>
            <Switch>
               <Route path="/c/:id/as-equipment" exact component={EquipmentRoute} />
               <Route path="/">
                  <AuthenticationProvider
                     configuration={ocidConfig}
                     loggerLevel={oidcLog.ERROR}
                     isEnabled
                     callbackComponentOverride={AuthCallback}
                     notAuthenticated={NotAuthenticated}
                     sessionLostComponent={SessionLostComponent}
                     authenticating={AuthenticatingComponent}
                  >
                     <OidcSecure>
                        <AuthenticatedRoutes />
                        <RedirectToConference />
                     </OidcSecure>
                  </AuthenticationProvider>
               </Route>
            </Switch>
         </BrowserRouter>
      </ThemeProvider>
   );
}

function MaterialUiToaster() {
   const { classes } = useStyles();

   return <Toaster position="top-center" toastOptions={{ className: classes.toast }} />;
}

export default App;
