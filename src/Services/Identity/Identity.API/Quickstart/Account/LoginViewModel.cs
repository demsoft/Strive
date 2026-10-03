// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Identity.API.Quickstart.Account
{
    public class LoginViewModel : LoginInputModel
    {
        public bool AllowRememberLogin { get; set; } = true;
        public bool EnableLocalLogin { get; set; } = true;

        /// <summary>Real accounts (email, password, registration) instead of the demo sign in.</summary>
        public bool AccountsMode { get; set; }

        public bool GoogleEnabled { get; set; }
        public bool ShowResendConfirmation { get; set; }

        /// <summary>A message from another page of the flow (a confirmation, a changed password).</summary>
        public string Notice { get; set; }

        public string Error { get; set; }
    }
}