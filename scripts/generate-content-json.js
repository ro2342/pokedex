// Generates uwp/PokedexUWP/Data/content.json from www/js/data.js, since
// the C# side can't execute JavaScript. Run manually whenever data.js
// changes: `node scripts/generate-content-json.js`.
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const dataJsPath = path.join(__dirname, '..', 'www', 'js', 'data.js');
const outPath = path.join(__dirname, '..', 'uwp', 'PokedexUWP', 'Data', 'content.json');

const source = fs.readFileSync(dataJsPath, 'utf8') +
  '\nthis.POKEMON = POKEMON; this.GAMES = GAMES; this.CHIP_IDS = CHIP_IDS;';
const sandbox = {};
vm.createContext(sandbox);
vm.runInContext(source, sandbox);

const setToArray = (s) => (s ? Array.from(s).sort((a, b) => a - b) : null);

const games = sandbox.GAMES.map((g) => ({
  id: g.id,
  name: g.name,
  sub: g.sub,
  color: g.color,
  label: g.label,
  oneway: g.oneway,
  dex: setToArray(g.dex),
}));

const content = {
  pokemon: sandbox.POKEMON,
  games,
  chipIds: sandbox.CHIP_IDS,
};

fs.mkdirSync(path.dirname(outPath), { recursive: true });
fs.writeFileSync(outPath, JSON.stringify(content, null, 2));
console.log(`Wrote ${sandbox.POKEMON.length} pokemon, ${games.length} games -> ${path.relative(process.cwd(), outPath)}`);
