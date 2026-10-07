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
