// the page is only shown when other clients are still told about the sign out in a hidden frame: go back at once
(function () {
    var a = document.querySelector("a.PostLogoutRedirectUri");
    if (a) {
        window.location.replace(a.href);
    }
})();
