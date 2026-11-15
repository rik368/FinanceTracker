const { chromium, firefox, webkit } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs/promises');
const path = require('node:path');

(async () => {
    const engine = process.env.BROWSER_ENGINE || 'chromium';
    const label = process.env.BROWSER_CHANNEL || (engine === 'chromium' ? 'msedge' : engine);
    const browser = await ({ chromium, firefox, webkit })[engine].launch(engine === 'chromium' ? { channel: label } : {});
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    const root = process.env.BASE_URL || 'http://localhost:5142';
    const email = 'browser-test-' + process.pid + '-' + Date.now() + '@example.test';
    const password = 'Browser-Test2026!';
    try {
        await page.goto(root + '/Account/Register');
        await page.locator('input[name="Input.Email"]').fill(email);
        await page.locator('input[name="Input.Password"]').fill(password);
        await page.locator('input[name="Input.ConfirmPassword"]').fill(password);
        await page.getByRole('button', { name: 'Register', exact: true }).click();
        await page.getByRole('button', { name: 'Load sample data', exact: true }).click();
        await page.getByText('Sample data loaded.', { exact: true }).waitFor();
        await page.getByRole('button', { name: 'Transactions', exact: true }).click();
        await page.getByLabel('Search transactions').fill('taxi');
        await page.waitForFunction(() => document.querySelectorAll('.item').length === 1);
        await page.locator('.item').filter({ hasText: 'Taxi' }).waitFor();
        assert.equal(await page.locator('.item').count(), 1);
        await page.getByLabel('Search transactions').fill('');
        await page.getByRole('button', { name: 'Add transaction', exact: true }).click();
        await page.getByRole('button', { name: 'Save', exact: true }).click();
        await page.getByText('Choose a category.', { exact: true }).waitFor();
        await page.locator('form select').selectOption({ label: 'Groceries (Expense)' });
        await page.getByLabel('Amount, KZT').fill('25.50');
        await page.getByLabel('Note', { exact: true }).fill('Browser verification');
        await page.getByRole('button', { name: 'Save', exact: true }).click();
        const item = page.locator('.item').filter({ hasText: 'Browser verification' });
        await item.waitFor();
        await item.getByRole('button', { name: 'Edit', exact: true }).click();
        await page.getByLabel('Amount, KZT').fill('30');
        await page.getByRole('button', { name: 'Save', exact: true }).click();
        await item.getByText(/30.00/).waitFor();
        await item.getByRole('button', { name: 'Delete', exact: true }).click();
        await page.getByRole('group', { name: 'Confirm deletion' })
            .getByRole('button', { name: 'Delete', exact: true }).click();
        await item.waitFor({ state: 'hidden' });
        const download = page.waitForEvent('download');
        await page.getByRole('link', { name: 'Export month CSV' }).click();
        assert.match((await download).suggestedFilename(), /^transactions-\d{4}-\d{2}\.csv$/);
        for (const width of [375, 768, 1280]) {
            await page.setViewportSize({ width, height: 900 });
            for (const name of ['Overview', 'Transactions', 'Categories', 'Budgets', 'Analytics']) {
                await page.getByRole('button', { name, exact: true }).click();
                await page.getByRole('heading', { name, exact: true }).waitFor();
                assert.equal(await page.evaluate(() => document.documentElement.scrollWidth), width,
                    name + ' overflows at ' + width);
            }
        }
        await page.getByRole('button', { name: 'Overview', exact: true }).click();
        const output = path.join(__dirname, '..', 'verification');
        await fs.mkdir(output, { recursive: true });
        await page.screenshot({ path: path.join(output, label + '-desktop.png'), fullPage: true });
        await page.setViewportSize({ width: 375, height: 812 });
        await page.screenshot({ path: path.join(output, label + '-mobile.png'), fullPage: true });
        await page.getByRole('button', { name: 'Sign out', exact: true }).click();
        await page.getByRole('link', { name: 'Sign in', exact: true }).waitFor();
        await page.goto(root + '/Account/Login');
        await page.locator('input[name="Input.Email"]').fill(email);
        await page.locator('input[name="Input.Password"]').fill(password);
        await page.getByRole('button', { name: 'Log in', exact: true }).click();
        await page.getByRole('heading', { name: 'Overview', exact: true }).waitFor();
        await page.goto(root + '/Account/Manage/DeletePersonalData');
        await page.locator('input[name="Input.Password"]').fill(password);
        await page.getByRole('button', { name: 'Delete data and close my account', exact: true }).click();
        await page.getByRole('link', { name: 'Sign in', exact: true }).waitFor();
        await page.goto(root + '/Account/Login');
        await page.locator('input[name="Input.Email"]').fill(email);
        await page.locator('input[name="Input.Password"]').fill(password);
        await page.getByRole('button', { name: 'Log in', exact: true }).click();
        await page.getByText('Error: Invalid login attempt.', { exact: true }).waitFor();
        await fs.writeFile(path.join(output, label + '-result.json'), JSON.stringify({ browser: label, version: browser.version(), platform: process.platform, passed: true, date: new Date().toISOString(), widths: [375,768,1280], scenarios: ['registration','sample','search','validation','CRUD','CSV','responsive tabs','logout','login','account deletion','deleted login rejected'] }, null, 2));
        console.log('PASS registration, sample data, search, validation, CRUD, CSV, mobile width and login.');
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
