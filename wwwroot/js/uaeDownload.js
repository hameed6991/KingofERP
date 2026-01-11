window.uaeDownload = {
    downloadText: function (filename, content, mime) {
        const blob = new Blob([content], { type: mime || "text/plain;charset=utf-8" });
        const url = URL.createObjectURL(blob);

        const a = document.createElement("a");
        a.href = url;
        a.download = filename || "file.txt";
        document.body.appendChild(a);
        a.click();
        a.remove();

        URL.revokeObjectURL(url);
    }
};
