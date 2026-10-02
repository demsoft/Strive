/// <reference types="Cypress" />

describe("WebRTC", () => {
  it("Activate screen and check connection status", () => {
    cy.createAndJoinOpenedConference("Vincent");
    cy.get("#media-controls-troubleshooting").click();
    cy.get("#troubleshooting-connection-badge").contains("new");

    // close the dialog: MUI only reacts to key events that reach the dialog itself, not to ones fired on the body
    cy.get("#troubleshooting-dialog").trigger("keydown", { key: "Escape" });
    cy.get("#troubleshooting-dialog").should("not.exist");

    // The WebRTC connection to the SFU is set up shortly after joining (the app answers "Not connected" before).
    // Click only while screen sharing is inactive, and wait longer than it takes to activate, as every click toggles.
    cy.waitUntil(
      () =>
        cy.get("#media-controls-screen").then(($button) => {
          if ($button.hasClass("MuiFab-primary")) return true;
          return cy
            .wrap($button)
            .click()
            .then(() => false);
        }),
      { interval: 3000, timeout: 30000, errorMsg: "Screen sharing was not activated" }
    );

    cy.get("#media-controls-troubleshooting").click();
    cy.get("#troubleshooting-connection-badge")
      .contains("new")
      .should("not.exist");
  });
});
