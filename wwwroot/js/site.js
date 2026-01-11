document.addEventListener('mousedown', function (e) {
    // If click is NOT inside input/textarea/select/contenteditable
    const isEditable = e.target.closest('input, textarea, select, [contenteditable="true"]');
    if (!isEditable) {
        const a = document.activeElement;
        if (a && a.blur) a.blur();
    }
});
