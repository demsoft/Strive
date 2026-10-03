/// <reference types="Cypress" />

describe("Hand raise", () => {
  it("Raise and lower the hand", () => {
    cy.createAndJoinOpenedConference("Vincent");

    cy.get("#hand-raise-toggle").should("have.attr", "aria-pressed", "false");

    cy.get("#hand-raise-toggle").click();
    cy.get("#hand-raise-toggle").should("have.attr", "aria-pressed", "true");

    cy.get("#hand-raise-toggle").click();
    cy.get("#hand-raise-toggle").should("have.attr", "aria-pressed", "false");
  });
});
