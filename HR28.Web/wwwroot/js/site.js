// HR28 shared scripts (loaded on every signed-in page).

// Copy buttons next to ID card numbers (Views/Shared/_CopyId.cshtml).
(function () {
    // Older browsers, or when the clipboard is refused: copy through a hidden text box.
    function copyWithTextBox(text) {
        var box = document.createElement("textarea");
        box.value = text;
        box.setAttribute("readonly", "");
        box.style.position = "fixed";
        box.style.opacity = "0";
        document.body.appendChild(box);
        box.select();
        var ok = false;
        try { ok = document.execCommand("copy"); } catch (e) { ok = false; }
        document.body.removeChild(box);
        return ok ? Promise.resolve() : Promise.reject();
    }

    function copyText(text) {
        if (navigator.clipboard && window.isSecureContext) {
            return navigator.clipboard.writeText(text).catch(function () {
                return copyWithTextBox(text);
            });
        }

        return copyWithTextBox(text);
    }

    document.addEventListener("click", function (e) {
        var button = e.target.closest(".ui-copy-btn");
        if (!button) return;

        // Don't open the row's voter profile when copying.
        e.preventDefault();
        e.stopPropagation();

        var label = button.getAttribute("aria-label");

        copyText(button.getAttribute("data-copy") || "").then(function () {
            button.classList.add("is-copied");
            button.setAttribute("title", "Copied");
            button.setAttribute("aria-label", "Copied");
        }, function () {
            button.setAttribute("title", "Could not copy. Select the number and copy it instead.");
        });

        clearTimeout(button._copyTimer);
        button._copyTimer = setTimeout(function () {
            button.classList.remove("is-copied");
            button.setAttribute("title", "Copy ID card number");
            button.setAttribute("aria-label", label);
        }, 1500);
    }, true);
})();

// File boxes with data-max-bytes: say at once when a file is too large, before uploading.
// The server checks again.
(function () {
    document.addEventListener("change", function (e) {
        var input = e.target;
        if (!(input instanceof HTMLInputElement) || input.type !== "file" || !input.dataset.maxBytes) return;

        var max = parseInt(input.dataset.maxBytes, 10);
        var file = input.files && input.files[0];
        var error = document.getElementById(input.getAttribute("aria-describedby") || "");

        if (file && file.size > max) {
            var mb = function (bytes) { return (bytes / (1024 * 1024)).toFixed(1).replace(/\.0$/, ""); };
            if (error) {
                error.textContent = "This photo is " + mb(file.size) + " MB. Please choose one under " + mb(max) + " MB.";
                error.hidden = false;
            }
            input.value = "";
            input.setAttribute("aria-invalid", "true");
        } else if (error) {
            error.hidden = true;
            error.textContent = "";
            input.removeAttribute("aria-invalid");
        }
    });
})();

// Status pop-up (Shared/_StatusButton + _StatusDialog): click a voter's status to change it.
(function () {
    document.addEventListener("click", function (e) {
        var btn = e.target.closest(".h28-status-btn");
        var dialog = document.getElementById("h28StatusDialog");
        if (!btn || !dialog) return;

        // Don't open the row's voter profile.
        e.preventDefault();
        e.stopPropagation();

        document.getElementById("h28StatusVoterId").value = btn.dataset.id;
        document.getElementById("h28StatusVoterName").textContent = btn.dataset.name;
        dialog.querySelectorAll('input[name="status"]').forEach(function (r) {
            r.checked = r.value === btn.dataset.status;
        });
        dialog.showModal();
    }, true);

    document.addEventListener("click", function (e) {
        if (e.target.id !== "h28StatusCancel") return;
        var dialog = document.getElementById("h28StatusDialog");
        if (dialog) dialog.close();
    });
})();

// Session timeout (Shared/_SessionTimeout): after an hour with no activity, ask
// "Are you still there?"; with no answer within a minute, sign out with a message.
// Activity in any tab counts; while someone works, the session is quietly refreshed.
(function () {
    var cfg = document.getElementById("h28SessionConfig");
    var dialog = document.getElementById("h28SessionDialog");
    if (!cfg || !dialog) return;

    var idleMs = parseInt(cfg.dataset.idleSeconds, 10) * 1000;
    var warnMs = parseInt(cfg.dataset.warnSeconds, 10) * 1000;
    var refreshEveryMs = 5 * 60 * 1000;
    var storageKey = "hr28.lastActivity";
    var countdown = document.getElementById("h28SessionCountdown");
    var leaving = false;

    function now() { return Date.now(); }

    // Shared by all tabs, so working in one keeps the others signed in too.
    var localLast = now();
    function shared() { try { return parseInt(localStorage.getItem(storageKey), 10) || 0; } catch (e) { return 0; } }
    function markActive() { localLast = now(); try { localStorage.setItem(storageKey, String(localLast)); } catch (e) { } }
    function lastActive() { return Math.max(localLast, shared()); }

    // This page load reached the server, so the session is fresh.
    markActive();
    var lastRefresh = now();

    function antiForgery() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : "";
    }

    // Restarts the hour on the server and renews the sign-in. False: the session has ended.
    function refresh() {
        lastRefresh = now();
        return fetch(cfg.dataset.keepaliveUrl, {
            method: "POST",
            credentials: "same-origin",
            headers: { "RequestVerificationToken": antiForgery() }
        }).then(function (r) {
            if (r.status === 401) { goToSignIn(); return false; }
            return r.ok;
        }).catch(function () { return true; });
    }

    // Unanswered question: sign out properly; the sign-in page explains why.
    function signOut(idle) {
        if (leaving) return;
        leaving = true;

        var form = document.createElement("form");
        form.method = "post";
        form.action = cfg.dataset.logoutUrl;

        [["__RequestVerificationToken", antiForgery()], ["reason", idle ? "idle" : ""]].forEach(function (pair) {
            var input = document.createElement("input");
            input.type = "hidden";
            input.name = pair[0];
            input.value = pair[1];
            form.appendChild(input);
        });

        document.body.appendChild(form);
        form.submit();
    }

    // The session had already ended on the server: the sign-in page says so.
    function goToSignIn() {
        if (leaving) return;
        leaving = true;
        window.location.href = cfg.dataset.loginUrl;
    }

    ["keydown", "mousedown", "touchstart", "scroll", "input"].forEach(function (type) {
        document.addEventListener(type, function () {
            if (dialog.open || leaving) return;
            markActive();
            if (now() - lastRefresh > refreshEveryMs) refresh();
        }, { passive: true, capture: true });
    });

    document.getElementById("h28SessionStay").addEventListener("click", function () {
        refresh().then(function (ok) {
            if (!ok) return;
            markActive();
            dialog.close();
        });
    });

    document.getElementById("h28SessionLeave").addEventListener("click", function () { signOut(false); });

    // Escape doesn't dismiss the question; one of the buttons must be chosen.
    dialog.addEventListener("cancel", function (e) { e.preventDefault(); });

    setInterval(function () {
        if (leaving) return;

        var idleFor = now() - lastActive();

        if (idleFor >= idleMs) {
            signOut(true);
        } else if (idleFor >= idleMs - warnMs) {
            var seconds = Math.max(1, Math.ceil((idleMs - idleFor) / 1000));
            countdown.textContent = seconds + (seconds === 1 ? " second" : " seconds");
            if (!dialog.open) dialog.showModal();
        } else if (dialog.open) {
            // Someone carried on in another tab.
            dialog.close();
        }
    }, 1000);
})();
