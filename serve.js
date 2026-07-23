const http = require('http');
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, 'www');
const rootImg = path.join(__dirname, 'img');
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.png': 'image/png' };

http.createServer((req, res) => {
  let url = decodeURIComponent(req.url.split('?')[0]);
  let filePath;
  if (url.startsWith('/img/')) {
    filePath = path.join(rootImg, url.slice(4));
  } else {
    filePath = path.join(root, url === '/' ? 'index.html' : url);
  }
  fs.readFile(filePath, (err, data) => {
    if (err) { res.writeHead(404); res.end('not found: ' + filePath); return; }
    const ext = path.extname(filePath);
    res.writeHead(200, { 'Content-Type': mime[ext] || 'application/octet-stream' });
    res.end(data);
  });
}).listen(8090, () => console.log('serving on http://localhost:8090'));
