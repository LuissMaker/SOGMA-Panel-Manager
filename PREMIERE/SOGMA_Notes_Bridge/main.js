const status = document.getElementById("status");
const list = document.getElementById("list");
const open = document.getElementById("open");

function render(data) {
  list.innerHTML = "";
  const panels = Array.isArray(data?.panels) ? data.panels : [];
  status.textContent = panels.length ? panels.length + " panel(es) con nota" : "No hay notas para Premiere.";
  for (const panel of panels) {
    const item = document.createElement("div");
    item.className = "item";
    const name = document.createElement("div");
    name.className = "name";
    name.textContent = panel.fileName || panel.sourceFileName || "Panel";
    const note = document.createElement("div");
    note.className = "note";
    note.textContent = panel.note || "";
    item.append(name, note);
    list.appendChild(item);
  }
}

open.addEventListener("click", async () => {
  status.textContent = "Selecciona SOGMA_PREMIERE_NOTES.json…";
  try {
    const fs = require("uxp").storage.localFileSystem;
    const file = await fs.getFileForOpening({ types: ["json"] });
    if (!file) return;
    const text = await file.read();
    render(JSON.parse(text));
  } catch (error) {
    status.textContent = "No se pudo abrir el archivo: " + error.message;
  }
});
