window.uae2fa = {
    renderQr: function (targetId, text) {
        const el = document.getElementById(targetId);
        if (!el) return;
        el.innerHTML = "";
        new QRCode(el, {
            text: text,
            width: 190,
            height: 190,
            correctLevel: QRCode.CorrectLevel.M
        });
    }
};
