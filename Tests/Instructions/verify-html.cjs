// Run with bundled Node and NODE_PATH pointing at the bundled node_modules.
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const { pathToFileURL, fileURLToPath } = require('url');

(async () => {
  const folder = path.resolve('Docs/PluginInstructions');
  const output = path.resolve('outputs/instructions'); fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let checks = 0;
  try {
    for (const width of [1440, 390]) {
      const page = await browser.newPage({ viewport: { width, height: 1000 }, deviceScaleFactor: 1 });
      for (const file of ['IDEOLOGIST_HTML_Instruktsii.html', 'IDEOLOGIST_HTML_Zapolnenie_parametrov.html']) {
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
        if (state.placeholders || state.details.some(Boolean) || errors.length) throw Error('Template placeholders, expanded scenarios or script error');
        for (const reference of state.references) {
          const url = new URL(reference);
          if (url.protocol !== 'file:') throw Error('Unexpected external dependency: ' + reference);
          if (!fs.existsSync(fileURLToPath(url))) throw Error('Missing local reference: ' + reference);
          if (url.hash && fileURLToPath(url) === path.join(folder, file) && !state.ids.includes(decodeURIComponent(url.hash.slice(1)))) throw Error('Missing anchor: ' + reference);
        }
        const guide = file.includes('Zapolnenie');
        await page.screenshot({ path: path.join(output, `${guide ? 'guide' : 'index'}-${width}.png`), fullPage: !guide });
        if (guide) {
          await page.locator('#scenarios').screenshot({ path: path.join(output, `scenarios-${width}.png`) });
          const summary = page.locator('#scenario-manual summary'); await summary.focus(); await page.keyboard.press('Enter');
          if (!(await page.locator('#scenario-manual').evaluate(n => n.open))) throw Error('Scenario cannot be opened with keyboard');
          await page.locator('#scenario-manual').screenshot({ path: path.join(output, `manual-${width}.png`) });
          await page.locator('#scenario-groups summary').click();
          await page.locator('#scenario-groups').screenshot({ path: path.join(output, `groups-${width}.png`) });
          await page.locator('#errors').screenshot({ path: path.join(output, `errors-${width}.png`) });
        }
        checks++; console.log('PASS:', state.title, width, 'px: local links, layout, collapsed scenarios, keyboard');
      }
      await page.close();
    }
    console.log('All', checks, 'HTML viewport checks passed.');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exit(1); });
