import '@fontsource/inter/400.css';
import '@fontsource/inter/500.css';
import '@fontsource/inter/600.css';
import '@fontsource/inter/700.css';
import '@fontsource/inter/800.css';
import { CssBaseline, ThemeProvider } from '@mui/material';
import { makeStyles } from 'tss-react/mui';
import { Toaster } from 'react-hot-toast';
import { BrowserRouter, Route, Switch } from 'react-router-dom';
import RequireAuth from './features/auth/components/RequireAuth';
import StriveAuthProvider from './features/auth/components/StriveAuthProvider';
import UserInteractionListener from './features/media/components/UserInteractionListener';
import theme from './theme';
import RecordingRoute from './routes/RecordingRoute';
import SharedRecordingRoute from './routes/SharedRecordingRoute';
import RedirectToConference from './RedirectToConference';
import AuthenticatedRoutes from './routes/AuthenticatedRoutes';
import EquipmentRoute from './routes/EquipmentRoute';

const useStyles = makeStyles()((theme) => ({
   toast: {
      backgroundColor: theme.palette.background.paper,
      color: theme.palette.text.primary,
   },
}));

function App() {
   return (
      <ThemeProvider theme={theme}>
         <MaterialUiToaster />
         <UserInteractionListener />
         <CssBaseline />
         <BrowserRouter>
            <Switch>
               <Route path="/c/:id/as-equipment" exact component={EquipmentRoute} />
               {/* opened by the recorder service, it joins with its own token */}
               <Route path="/c/:id/recording" exact component={RecordingRoute} />
               <Route path="/">
                  <StriveAuthProvider>
                     <Switch>
                        {/* the silent renew iframe, the provider processes the callback */}
                        <Route path="/authentication/silent_callback" render={() => null} />
                        {/* viewable without signing in if the recording is shared with everybody */}
                        <Route path="/r/:token" component={SharedRecordingRoute} />
                        <Route>
                           <RequireAuth>
                              <AuthenticatedRoutes />
                              <RedirectToConference />
                           </RequireAuth>
                        </Route>
                     </Switch>
                  </StriveAuthProvider>
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
