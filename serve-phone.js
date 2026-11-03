// Serves Builds/WebGL to phones on the same Wi-Fi:  node serve-phone.js [port]
// Sends the Brotli/gzip build files with the right Content-Encoding so Unity skips its slow JS decompression,
// and prints the LAN address to open on the phone (allow Node through the Windows firewall when asked).
const http = require("http"), fs = require("fs"), path = require("path"), os = require("os");
const root = path.join(__dirname, "Builds", "WebGL"), port = +process.argv[2] || 8765;
const types = { ".html": "text/html; charset=utf-8", ".js": "application/javascript", ".wasm": "application/wasm", ".data": "application/octet-stream", ".json": "application/json", ".png": "image/png", ".ico": "image/x-icon" };

http.createServer((req, res) => {
  let rel = decodeURIComponent(req.url.split("?")[0]); if (rel.endsWith("/")) rel += "index.html";
  const file = path.normalize(path.join(root, rel));
  if (!file.startsWith(root)) { res.writeHead(403); return res.end(); }
  fs.stat(file, (err, st) => {
    if (err || !st.isFile()) { res.writeHead(404); return res.end("not found"); }
    let name = file, headers = { "Content-Length": st.size, "Cache-Control": "no-cache" };
    for (const [ext, enc] of [[".br", "br"], [".gz", "gzip"]]) if (name.endsWith(ext)) { headers["Content-Encoding"] = enc; name = name.slice(0, -ext.length); }
    headers["Content-Type"] = types[path.extname(name)] || "application/octet-stream";
    res.writeHead(200, headers); fs.createReadStream(file).pipe(res);
  });
}).listen(port, "0.0.0.0", () => {
  console.log("Nova Blade WebGL server running. Open on your phone (same Wi-Fi):");
  for (const list of Object.values(os.networkInterfaces())) for (const a of list) if (a.family === "IPv4" && !a.internal) console.log(`  http://${a.address}:${port}/`);
  console.log(`  this PC: http://localhost:${port}/`);
});
