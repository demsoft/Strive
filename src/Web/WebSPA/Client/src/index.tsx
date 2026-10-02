import 'fontsource-roboto';
import React from 'react';
import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import App from './App';
import * as serviceWorker from './serviceWorker';
import store from './store';
import debug from 'debug';
import './services/i18n';

window.AudioContext = window.AudioContext || (window as any).webkitAudioContext;

debug.log = console.info.bind(console);

createRoot(document.getElementById('root')!).render(
   <React.StrictMode>
      <Provider store={store}>
         <App />
      </Provider>
   </React.StrictMode>,
);

// If you want your app to work offline and load faster, you can change
// unregister() to register() below. Note this comes with some pitfalls.
// Learn more about service workers: https://bit.ly/CRA-PWA
serviceWorker.unregister();
