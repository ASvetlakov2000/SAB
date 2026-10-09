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
  const density = [];
  try {
    for (const width of [1440, 390]) {
      const page = await browser.newPage({ viewport: { width, height: 1000 }, deviceScaleFactor: 1 });
      for (const file of files) {
        const errors = []; page.on('pageerror', e => errors.push(e.message));
        await page.goto(pathToFileURL(path.join(folder, file)).href); await page.waitForLoadState('load');
        const compact = page.locator('link[href="assets/compact.css"]');
        if (await compact.count() !== 1) throw Error(file + ': compact styles missing');
        // Compare identical content with and without the density override.
        const measure = () => page.evaluate(() => ({
          height: document.documentElement.scrollHeight,
          width: document.documentElement.scrollWidth,
          font: parseFloat(getComputedStyle(document.body).fontSize),
          identity: ['body', '.hero', 'h1', 'h2', '.section__inner', '.content-card', '.index-list'].map(selector => {
            const node = document.querySelector(selector);
            if (!node) return null;
            const s = getComputedStyle(node);
            return [selector, s.fontSize, s.fontFamily, s.lineHeight, s.color, s.backgroundColor,
              s.borderRadius, s.width, s.display, s.gridTemplateColumns];
          }),
          sectionPadding: parseFloat(getComputedStyle(document.querySelector('.section__inner')).paddingTop),
          cardPadding: document.querySelector('.content-card') ? parseFloat(getComputedStyle(document.querySelector('.content-card')).paddingTop) : null
        }));
        await compact.evaluate(n => n.disabled = true);
        const before = await measure();
        await page.locator('details').evaluateAll(nodes => nodes.forEach(n => n.open = true));
        const beforeExpanded = await measure();
        await compact.evaluate(n => n.disabled = false);
        await page.waitForFunction(() => {
          const sheet = document.querySelector('link[href="assets/compact.css"]').sheet;
          return sheet && !sheet.disabled;
        });
        const afterExpanded = await measure();
        if (afterExpanded.width > width + 1) throw Error(file + ': expanded scenario overflow at ' + width);
        const capsules = await page.evaluate(() => {
          const actions = [...document.querySelectorAll('main .cmd')];
          const labels = [...document.querySelectorAll('main .kbd')];
          return {
            actions: actions.length, labels: labels.length,
            badAction: actions.some(n => getComputedStyle(n).backgroundColor !== 'rgb(245, 245, 247)' || getComputedStyle(n).color !== 'rgb(29, 29, 31)'),
            badLabel: labels.some(n => getComputedStyle(n).backgroundColor !== 'rgba(0, 0, 0, 0)'),
            nested: document.querySelectorAll('.cmd .cmd,.cmd .kbd,.kbd .cmd,.kbd .kbd').length,
            rawLabels: [...document.querySelectorAll('main p, main li, main td')].some(n => [...n.childNodes].some(c => c.nodeType === Node.TEXT_NODE && /«[^»]+»/.test(c.textContent))),
            rowSpacing: [...document.querySelectorAll('.steps li')].map(n => {
              const s = getComputedStyle(n); return [s.paddingTop, s.paddingBottom, s.marginTop, s.marginBottom, s.minHeight].join('|');
            })
          };
        });
        if (!capsules.actions || !capsules.labels || capsules.badAction || capsules.badLabel || capsules.nested || capsules.rawLabels) throw Error(file + ': inconsistent capsule roles ' + JSON.stringify(capsules));
        if (new Set(capsules.rowSpacing).size !== 1 || !capsules.rowSpacing[0].endsWith('|0px|0px|0px')) throw Error(file + ': unequal single/multiple step spacing');
        await page.locator('details').evaluateAll(nodes => nodes.forEach(n => n.open = false));
        const after = await measure();
        if (after.font !== before.font || after.height >= before.height || Math.abs(after.sectionPadding / before.sectionPadding - 0.7) > 0.001) throw Error(file + ': spacing must be reduced by 30% with original body type');
        if (after.cardPadding !== null && Math.abs(after.cardPadding / before.cardPadding - 0.7) > 0.001) throw Error(file + ': wrong card padding');
        if (JSON.stringify(after.identity) !== JSON.stringify(before.identity)) throw Error(file + ': original typography or layout changed at ' + width + ' ' + JSON.stringify({before: before.identity, after: after.identity}));
        density.push({file, width, before: before.height, after: after.height,
          reduction: Math.round(100 * (1 - after.height / before.height)),
          expandedBefore: beforeExpanded.height, expandedAfter: afterExpanded.height});
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
          const columns = await page.locator('.index-list').first().evaluate(n => getComputedStyle(n).gridTemplateColumns.split(' ').length);
          if (columns !== 1) throw Error('Catalog must retain the original single column');
        } else {
          for (const id of ['requirements', 'procedure', 'check', 'errors']) if (!state.ids.includes(id)) throw Error(file + ': missing user workflow section ' + id);
        }
        const guide = file.includes('Zapolnenie');
        if (guide || file === 'SAB_HTML_Instruktsii.html') {
          await page.evaluate(() => scrollTo({ top: 0, behavior: 'instant' }));
          await page.screenshot({ path: path.join(output, `${guide ? 'guide' : 'index'}-${width}.png`) });
        }
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
    fs.writeFileSync(path.join(output, 'density.json'), JSON.stringify(density, null, 2));
    for (const item of density.filter(n => /Instruktsii|Zapolnenie/.test(n.file))) console.log('DENSITY:', item.file, item.width, `${item.before} → ${item.after} px (-${item.reduction}%)`);
    console.log('All', checks, 'HTML viewport checks passed.');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exit(1); });
