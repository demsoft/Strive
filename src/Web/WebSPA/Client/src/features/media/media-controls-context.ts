import React from 'react';

export type MediaControlsContextType = {
   leftControlsContainer?: HTMLElement | null;
};

const MediaControlsContext = React.createContext<MediaControlsContextType>({});

export default MediaControlsContext;
