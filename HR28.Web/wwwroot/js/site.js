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
