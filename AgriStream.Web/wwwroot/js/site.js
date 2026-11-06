// Show any toast notifications rendered by the server.
document.querySelectorAll('.toast').forEach(function (el) {
    bootstrap.Toast.getOrCreateInstance(el).show();
});

// Confirm dialogs for destructive buttons: <button data-confirm="Are you sure?">
document.addEventListener('submit', function (e) {
    var btn = e.submitter;
    if (btn && btn.dataset.confirm && !window.confirm(btn.dataset.confirm)) {
        e.preventDefault();
    }
});

// Disable submit buttons after click to avoid double posts, and show a little spinner.
document.addEventListener('submit', function (e) {
    var btn = e.submitter;
    if (!e.defaultPrevented && btn && btn.dataset.loading) {
        setTimeout(function () {
            btn.disabled = true;
            btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>' + btn.dataset.loading;
        }, 0);
    }
});
