// Cypress 10+ configuration (replaces cypress.json). The browser is selected on the command line (--browser chrome).
module.exports = {
   projectId: 'coci4n',
   hosts: {
      '*.localhost': '127.0.0.1',
   },
   chromeWebSecurity: false,
   defaultCommandTimeout: 10000,
   e2e: {
      baseUrl: 'https://localhost/',
      specPattern: 'cypress/integration/**/*.js',
      supportFile: 'cypress/support/index.js',
      setupNodeEvents(on, config) {
         return require('./cypress/plugins/index.js')(on, config);
      },
   },
};
