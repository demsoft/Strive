/// <reference types="Cypress" />

describe("Pre-join", () => {
  it("Check the devices before joining and join", () => {
    cy.loginAndLoadMainSite("Vincent");
    cy.get("#create-conference-button").click();
    cy.get("button[type=submit]").click();
    cy.get("#join-conference-button").click();

    // the camera and microphone are off until the participant turns them on
    cy.get("#pre-join").should("be.visible");
    cy.get("#pre-join-toggle-mic").should("have.attr", "aria-pressed", "false");
    cy.get("#pre-join-toggle-webcam").should("have.attr", "aria-pressed", "false");

    cy.get("#pre-join-toggle-mic").click();
    cy.get("#pre-join-toggle-mic").should("have.attr", "aria-pressed", "true");
    cy.get("#pre-join-toggle-mic").click();

    cy.get("#pre-join-join-button").click();
    cy.get("#moderator-open-conference-button").should("be.visible");
  });
});
