// window.ENV is normally injected by the ASP.NET host (/config.js)
(window as any).ENV = {
   identityUrl: 'http://localhost/identity',
   conferenceUrl: 'http://localhost/api',
   signalrHubUrl: 'http://localhost/api/signalr',
   equipmentSignalrHubUrl: 'http://localhost/api/equipment-signalr',
   frontendUrl: 'http://localhost',
   gitInfo: { commit: 'test', ref: 'test', timestamp: '' },
};
