/// <reference types="Cypress" />

describe("Reactions", () => {
  it("Send a reaction and see it", () => {
    cy.createAndJoinOpenedConference("Vincent");

    cy.get("#reactions-picker-toggle").click();
    cy.get("#reactions-picker").contains("🎉").closest("button").click();

    cy.get("#reactions-overlay").contains("🎉");
  });
});
