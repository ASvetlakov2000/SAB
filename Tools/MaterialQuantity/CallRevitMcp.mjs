import net from "node:net";
import fs from "node:fs/promises";

const [, , method, codePath, outputPath] = process.argv;
if (!method) {
  throw new Error("Usage: node CallRevitMcp.mjs <method> [codePath] [outputPath]");
}

const params = method === "send_code_to_revit"
  ? { code: await fs.readFile(codePath, "utf8"), transactionMode: "none" }
  : {};
const request = { jsonrpc: "2.0", method, params, id: `sab-${Date.now()}` };

const response = await new Promise((resolve, reject) => {
  const socket = net.createConnection({ host: "127.0.0.1", port: 8080 });
  let buffer = "";
  const timer = setTimeout(() => {
    socket.destroy();
    reject(new Error(`Revit MCP timeout: ${method}`));
  }, 70000);
  socket.on("connect", () => socket.write(JSON.stringify(request)));
  socket.on("data", chunk => {
    buffer += chunk.toString("utf8");
    try {
      const parsed = JSON.parse(buffer);
      clearTimeout(timer);
      socket.end();
      resolve(parsed);
    } catch (error) {
      if (!(error instanceof SyntaxError)) {
        clearTimeout(timer);
        socket.destroy();
        reject(error);
      }
    }
  });
  socket.on("error", error => {
    clearTimeout(timer);
    reject(error);
  });
});

if (response.error) throw new Error(JSON.stringify(response.error));
if (outputPath) {
  await fs.writeFile(outputPath, JSON.stringify(response.result, null, 2), "utf8");
  console.log(outputPath);
} else {
  console.log(JSON.stringify(response.result, null, 2));
}
