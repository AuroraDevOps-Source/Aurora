const mime = {pdf:"application/pdf", png:"image/png", jpg:"image/jpeg", jpeg:"image/jpeg", gif:"image/gif", bmp:"image/bmp", webp:"image/webp"};
export function fromUpload(input, text) {
    return source(document.getElementById(input)?.files?.[0] ?? new Blob([text], {type:"text/plain"}));
}
export function fromPdf(bytes) { return source(new Blob([bytes], {type:"application/pdf"})); }

function source(blob) {
    let url;
    return {
        async show(host, name) {
            const ext = name.split(".").pop().toLowerCase();
            const node = document.createElement("div");
            host.replaceChildren(node);
            if (mime[ext]) {
                url ??= URL.createObjectURL(blob.slice(0, blob.size, mime[ext]));
                const viewer = document.createElement(ext === "pdf" ? "iframe" : "img");
                viewer.title = name;
                viewer.alt = name;
                viewer.src = url;
                node.append(viewer);
            } else if (["docx","xlsx","tif","tiff"].includes(ext)) {
                const formats = await import("./document-vendor/formats.js");
                const buffer = await blob.arrayBuffer();
                if (ext === "tif" || ext === "tiff") {
                    let total = 0;
                    for (const frame of formats.tiff.decode(buffer)) {
                        if (!frame.t256 || !frame.t257) continue;
                        total += frame.t256[0] * frame.t257[0];
                        if (total > 64000000) throw new Error("Image exceeds the preview size limit.");
                        formats.tiff.decodeImage(buffer,frame);
                        const canvas = document.createElement("canvas");
                        canvas.width = frame.width;
                        canvas.height = frame.height;
                        const ctx = canvas.getContext("2d");
                        const pixels = ctx.createImageData(frame.width,frame.height);
                        pixels.data.set(formats.tiff.toRGBA8(frame));
                        ctx.putImageData(pixels,0,0);
                        node.append(canvas);
                    }
                } else {
                    renderOffice(node,buffer,formats,ext);
                }
            } else {
                const pre = document.createElement("pre");
                pre.textContent = await blob.text();
                node.append(pre);
            }
        },
        release() { if (url) URL.revokeObjectURL(url); url = undefined; }
    };
}
function renderOffice(host,buffer,formats,ext) {
    let size = 0;
    const entries = formats.unzipSync(new Uint8Array(buffer), {filter(entry) {
        if (!/^(word\/document\.xml|xl\/(workbook\.xml|_rels\/workbook\.xml\.rels|sharedStrings\.xml|worksheets\/sheet\d+\.xml))$/.test(entry.name)) return false;
        size += entry.originalSize;
        if (size > 32000000) throw new Error("Document exceeds the preview size limit.");
        return true;
    }});
    function xml(name) {
        if (!entries[name]) return null;
        return new DOMParser().parseFromString(formats.strFromU8(entries[name]),"application/xml");
    }
    const elements = (parent,tag) => Array.from(parent?.getElementsByTagNameNS("*",tag) ?? []);
    const textRuns = el => elements(el,"t").map(t=>t.textContent).join("");
    if (ext === "docx") {
        for (const paragraph of elements(xml("word/document.xml"),"p")) {
            const p = document.createElement("p");
            p.textContent = textRuns(paragraph);
            host.append(p);
        }
        return;
    }
    const strings = elements(xml("xl/sharedStrings.xml"),"si").map(textRuns);
    const rels = elements(xml("xl/_rels/workbook.xml.rels"),"Relationship");
    for (const sheet of elements(xml("xl/workbook.xml"),"sheet")) {
        const target = rels.find(r=>r.getAttribute("Id")===sheet.getAttribute("r:id"))?.getAttribute("Target");
        if (!target) continue;
        const heading = document.createElement("h3");
        heading.textContent = sheet.getAttribute("name");
        host.append(heading);
        const table = document.createElement("table");
        for (const row of elements(xml(target.startsWith("/")?target.slice(1):"xl/"+target),"row")) {
            const tr = table.insertRow();
            let col = 0;
            for (const cell of elements(row,"c")) {
                const letters = cell.getAttribute("r")?.match(/^[A-Z]+/)?.[0] ?? "";
                const index = Array.from(letters).reduce((i,c)=>26*i+c.charCodeAt(0)-64,0);
                if (index>col+1) tr.insertCell().colSpan=index-col-1;
                col = index;
                const value=elements(cell,"v")[0]?.textContent ?? "";
                tr.insertCell().textContent=cell.getAttribute("t")==="s" ? strings[Number(value)] : cell.getAttribute("t")==="inlineStr" ? textRuns(cell) : value;
            }
        }
        host.append(table);
    }
}
