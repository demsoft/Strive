import React from 'react';

export type ConferenceLayoutContextType = {
   sceneBarContainer?: HTMLElement | null;
   sceneBarWidth?: number;
   chatContainer?: HTMLElement | null;
   chatWidth: number;
};

const ConferenceLayoutContext = React.createContext<ConferenceLayoutContextType>({ chatWidth: 0 });

export default ConferenceLayoutContext;
