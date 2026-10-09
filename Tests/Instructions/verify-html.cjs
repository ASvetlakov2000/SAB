// Run with bundled Node and NODE_PATH pointing at the bundled node_modules.
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { pathToFileURL, fileURLToPath } = require('url');

(async () => {
  const folder = path.resolve('Docs/PluginInstructions');
  const output = path.resolve('outputs/instructions'); fs.mkdirSync(output, { recursive: true });
  const files = fs.readdirSync(folder).filter(f => f.endsWith('.html')).sort();
  if (files.length !== 23 || files.some(f => !f.startsWith('SAB_HTML_'))) throw Error('Expected SAB catalog and 22 current guides');
  const cssHash = crypto.createHash('sha256').update(fs.readFileSync(path.join(folder, 'assets/template.css'))).digest('hex');
  if (cssHash !== '78ffaf94af5fe1dbde191e97895d8abb0c3943f48277f9c4aea3d771ff176e1a') throw Error('Approved design CSS changed');
  const allHtml = files.map(f => fs.readFileSync(path.join(folder, f), 'utf8')).join('\n');
  const source = fs.readFileSync('SAB/Cls_RevitLibraryBuilder/UI/LibraryBuilderWindow.cs', 'utf8');
  const operations = [...source.matchAll(/CreateInfo\(LibraryToolId\.(\w+),/g)].map(m => m[1]);
  for (const operation of operations) {
    if (!allHtml.includes(`data-operation="${operation}"`)) throw Error('Undocumented library operation: ' + operation);
  }
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let checks = 0;
  try {
    for (const width of [1440, 390]) {
      const page = await browser.newPage({ viewport: { width, height: 1000 }, deviceScaleFactor: 1 });
      for (const file of files) {
        const errors = []; page.on('pageerror', e => errors.push(e.message));
        await page.goto(pathToFileURL(path.join(folder, file)).href); await page.waitForLoadState('load');
        const state = await page.evaluate(() => ({
          width: innerWidth, scroll: document.documentElement.scrollWidth,
          ids: [...document.querySelectorAll('[id]')].map(n => n.id),
          references: [...document.querySelectorAll('a[href],link[href],img[src]')].map(n => n.href || n.src),
          placeholders: document.querySelectorAll('[data-template-field]').length,
          details: [...document.querySelectorAll('details')].map(n => n.open),
          title: document.title
        }));
        if (state.scroll > state.width + 1) throw Error(file + ': horizontal page overflow at ' + width);
        if (new Set(state.ids).size !== state.ids.length) throw Error('Duplicate anchors');
        if (state.placeholders || state.details.some(Boolean) || errors.length || !state.title.startsWith('SAB —')) throw Error('Template placeholders, expanded scenarios, wrong branding or script error');
        for (const reference of state.references) {
          const url = new URL(reference);
          if (url.protocol !== 'file:') throw Error('Unexpected external dependency: ' + reference);
          if (!fs.existsSync(fileURLToPath(url))) throw Error('Missing local reference: ' + reference);
          if (url.hash) {
            const target = fs.readFileSync(fileURLToPath(url), 'utf8');
            if (!target.includes(`id="${decodeURIComponent(url.hash.slice(1))}"`)) throw Error('Missing anchor: ' + reference);
          }
        }
        if (file === 'SAB_HTML_Instruktsii.html') {
          const catalog = await page.locator('.index-link').evaluateAll(nodes => nodes.map(n => n.getAttribute('href')));
          if (catalog.length !== 22 || new Set(catalog).size !== 22 || files.some(f => f !== file && !catalog.includes(f))) throw Error('Catalog does not cover every current guide');
        } else {
          for (const id of ['requirements', 'procedure', 'check', 'errors']) if (!state.ids.includes(id)) throw Error(file + ': missing user workflow section ' + id);
        }
        const guide = file.includes('Zapolnenie');
        if (guide || file === 'SAB_HTML_Instruktsii.html') await page.screenshot({ path: path.join(output, `${guide ? 'guide' : 'index'}-${width}.png`), fullPage: !guide });
        if (guide) {
          await page.locator('#scenarios').screenshot({ path: path.join(output, `scenarios-${width}.png`) });
          const summary = page.locator('#scenario-manual summary'); await summary.focus(); await page.keyboard.press('Enter');
          if (!(await page.locator('#scenario-manual').evaluate(n => n.open))) throw Error('Scenario cannot be opened with keyboard');
          await page.locator('#scenario-manual').screenshot({ path: path.join(output, `manual-${width}.png`) });
          await page.locator('#scenario-groups summary').click();
          await page.locator('#scenario-groups').screenshot({ path: path.join(output, `groups-${width}.png`) });
          await page.locator('#errors').screenshot({ path: path.join(output, `errors-${width}.png`) });
        } else if (state.details.length) {
          const first = page.locator('details').first(); await first.locator('summary').focus(); await page.keyboard.press('Enter');
          if (!(await first.evaluate(n => n.open))) throw Error(file + ': scenario cannot be opened with keyboard');
          if (file.includes('Sistemnye') || file.includes('Oformlenie')) await first.screenshot({ path: path.join(output, `${path.basename(file, '.html')}-${width}.png`) });
        }
        page.removeAllListeners('pageerror');
        checks++; console.log('PASS:', state.title, width, 'px: local links, layout, collapsed scenarios, keyboard');
      }
      await page.close();
    }
    console.log('All', checks, 'HTML viewport checks passed.');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exit(1); });
