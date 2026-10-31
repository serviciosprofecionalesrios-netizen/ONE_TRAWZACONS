(function () {
    var uploading = false;

    document.addEventListener("submit", async function (event) {
        var form = event.target;
        if (form.id !== "toneladas-upload-form") return;
        event.preventDefault();
        if (uploading) return;

        uploading = true;
        var button = form.querySelector('[type="submit"]');
        var originalLabel = button.innerHTML;
        button.disabled = true;
        button.textContent = "Cargando Excel…";
        var feedback = document.createElement("div");
        feedback.className = "alert alert-info mt-2";
        feedback.setAttribute("role", "status");
        feedback.textContent = "Procesando archivo…";
        form.after(feedback);

        try {
            var response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                credentials: "same-origin"
            });
            if (!response.ok || response.redirected) {
                throw new Error("No se pudo completar la carga. Verifica tu sesión y vuelve a intentar.");
            }
            var page = new DOMParser().parseFromString(await response.text(), "text/html");
            var error = page.querySelector("[data-upload-error]");
            if (error) throw new Error(error.textContent.trim());
            var updated = page.getElementById("toneladas-content");
            var current = document.getElementById("toneladas-content");
            if (!updated || !current) throw new Error("No se pudo actualizar la información. Vuelve a intentar.");

            var scrollX = window.scrollX;
            var scrollY = window.scrollY;
            var active = current.querySelector(".quick-filter-btn.is-active");
            var range = active ? active.getAttribute("data-range") : "hoy";
            var dates = ["customRangeFrom", "customRangeTo"].map(function (id) {
                var input = document.getElementById(id);
                return input ? input.value : "";
            });
            current.replaceWith(updated);
            document.dispatchEvent(new Event("toneladas:updated"));
            ["customRangeFrom", "customRangeTo"].forEach(function (id, index) {
                var input = document.getElementById(id);
                if (input) input.value = dates[index];
            });
            var selected = Array.from(updated.querySelectorAll(".quick-filter-btn")).find(function (item) {
                return item.getAttribute("data-range") === range;
            });
            if (selected) selected.click();
            window.scrollTo({ left: scrollX, top: scrollY, behavior: "instant" });
        } catch (error) {
            feedback.className = "alert alert-danger mt-2";
            feedback.setAttribute("role", "alert");
            feedback.textContent = error.message || "No se pudo cargar el archivo. Vuelve a intentar.";
        } finally {
            uploading = false;
            button.disabled = false;
            button.innerHTML = originalLabel;
            if (feedback.classList.contains("alert-info")) feedback.remove();
        }
    });
})();
