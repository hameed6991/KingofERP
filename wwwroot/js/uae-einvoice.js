window.uaeEInvoice = {
    downloadTextFile: function (fileName, content) {
        const blob = new Blob([content], { type: "application/xml;charset=utf-8" });
        const url = URL.createObjectURL(blob);

        const a = document.createElement("a");
        a.href = url;
        a.download = fileName || "einvoice.xml";
        document.body.appendChild(a);
        a.click();

        setTimeout(() => {
            document.body.removeChild(a);
            URL.revokeObjectURL(url);
        }, 0);
    }
};
