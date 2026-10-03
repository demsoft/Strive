const { chromium } = require('playwright');
const fs = require('fs');
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const log = (...a) => console.log(new Date().toISOString().slice(11, 19), ...a);

const login = async (page, user) => {
  await page.goto('https://localhost/');
  await page.waitForURL(/identity\.localhost/, { timeout: 30000 });
  await page.fill('input[name=Username]', user);
  await page.fill('input[name=Password]', 'test');
  await page.click('button[value=login]');
  await page.waitForURL((u) => u.origin === 'https://localhost' && !u.search.includes('code='), { timeout: 60000 });
  await page.waitForTimeout(1500);
};

const joinConference = async (page) => {
  await page.waitForSelector('#pre-join', { timeout: 30000 });
  await page.click('#pre-join-toggle-webcam');
  await page.click('#pre-join-toggle-mic');
  await page.waitForTimeout(1500);
  await page.click('#pre-join-join-button');
};

(async () => {
  const browser = await chromium.launch({ args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream', '--ignore-certificate-errors', '--autoplay-policy=no-user-gesture-required'] });
  const mk = async () => (await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 900 }, permissions: ['camera', 'microphone'] })).newPage();

  // 1. moderator creates and opens a conference
  const mod = await mk();
  mod.on('pageerror', (e) => log('mod pageerror:', e.message.slice(0, 160)));
  await login(mod, 'Vincent');
  await mod.waitForSelector('#create-conference-button', { timeout: 30000 });
  for (let attempt = 0; attempt < 5; attempt++) {
    await mod.click('#create-conference-button');
    if (await mod.waitForSelector('button[type=submit]', { timeout: 8000 }).then(() => true).catch(() => false)) break;
  }
  await mod.click('button[type=submit]');
  await mod.click('#join-conference-button');
  const url = mod.url();
  await joinConference(mod);
  await mod.click('#moderator-open-conference-button');
  await mod.waitForSelector('#media-controls-webcam', { timeout: 30000 });
  log('moderator in conference', url);

  // 2. a participant joins with camera and microphone
  const guest = await mk();
  guest.on('pageerror', (e) => log('guest pageerror:', e.message.slice(0, 160)));
  await login(guest, 'Olaf');
  await guest.goto(url);
  await joinConference(guest);
  await guest.waitForSelector('#media-controls-webcam', { timeout: 30000 });
  log('guest in conference');
  await sleep(4000);

  // 3. the recording must be a deliberate choice: the button asks first
  await mod.click('#recording-toggle');
  await mod.waitForSelector('#recording-confirm', { timeout: 5000 });
  log('confirmation dialog shown');
  await mod.screenshot({ path: '/work/r1-confirm.png' });
  await mod.click('#recording-confirm-start');

  // 4. REC shows for everybody
  const recText = async (page) => (await page.locator('#recording-indicator').first().innerText().catch(() => '')).trim();
  const waitForRec = async (page, name) => {
    for (let i = 0; i < 120; i++) {
      const t = await recText(page);
      if (t.startsWith('REC')) { log(name, 'sees', JSON.stringify(t)); return; }
      await sleep(1000);
    }
    throw new Error(name + ' never saw REC (last: ' + (await recText(page)) + ')');
  };
  await waitForRec(mod, 'moderator');
  await waitForRec(guest, 'guest');
  await guest.screenshot({ path: '/work/r2-guest-rec.png' });

  // 5. record for a while, with some activity
  await guest.mouse.move(700, 600); await guest.click("#hand-raise-toggle", { timeout: 5000 }).catch(() => log("hand raise click skipped"));
  await sleep(8000);
  await guest.click("#reactions-picker-toggle", { timeout: 5000 }).catch(() => log("reaction click skipped"));
  await guest.locator('#reactions-picker button').first().click().catch(() => {});
  await sleep(12000);

  // 6. stop and wait until the recording is stored
  log('stopping');
  await mod.click('#recording-toggle');
  for (let i = 0; i < 240; i++) {
    const t = await recText(mod);
    if (!t) break;
    if (i % 10 === 0) log('moderator indicator:', JSON.stringify(t));
    await sleep(1000);
  }
  if (await recText(mod)) throw new Error('recording did not finish');
  log('recording finished');

  // 7. the recordings list has it, with a share link
  await mod.click('button[aria-label=more]');
  await mod.click('text=Recordings');
  await mod.waitForSelector('.recording-list-item', { timeout: 20000 });
  await mod.waitForTimeout(500);
  await mod.screenshot({ path: '/work/r3-list.png' });
  const itemText = await mod.locator('.recording-list-item').first().innerText();
  log('list item:', JSON.stringify(itemText.replace(/\n/g, ' | ')));
  const watch = await mod.locator('.recording-list-item a[aria-label=Watch]').first().getAttribute('href');
  log('share link', watch);

  // 8. signed-out visitors are asked to sign in
  const anon = await mk();
  await anon.goto(watch);
  await anon.waitForSelector('#shared-recording-login', { timeout: 20000 });
  log('anonymous visitor is asked to sign in');
  await anon.screenshot({ path: '/work/r4-anon.png' });

  // 9. a signed in user can watch it
  const viewer = await mk();
  await login(viewer, 'Olaf');
  await viewer.goto(watch);
  await viewer.waitForSelector('#shared-recording-video', { timeout: 30000 });
  await viewer.waitForFunction(() => { const v = document.querySelector('#shared-recording-video'); return v && v.readyState >= 1 && isFinite(v.duration); }, null, { timeout: 30000 });
  const info = await viewer.$eval('#shared-recording-video', (v) => ({ duration: v.duration, w: v.videoWidth, h: v.videoHeight, src: v.currentSrc }));
  log('player', JSON.stringify({ ...info, src: info.src.split('?')[0] }));
  // seeking works
  const seeked = await viewer.evaluate(async () => {
    const v = document.querySelector('#shared-recording-video');
    v.currentTime = Math.max(1, v.duration / 2);
    await new Promise((r) => v.addEventListener('seeked', r, { once: true }));
    return v.currentTime;
  });
  log('seeked to', seeked.toFixed(1));
  await viewer.screenshot({ path: '/work/r5-viewer.png' });

  // 10. download the file for inspection
  // through the browser context: it accepts the self-signed certificate of the development setup
  const res = await viewer.request.get(info.src);
  fs.writeFileSync('/work/recording.mp4', await res.body());
  log('downloaded recording.mp4', fs.statSync('/work/recording.mp4').size, 'bytes');
  await browser.close();
})().catch((e) => { console.log('FAILED:', e.message); process.exit(1); });
