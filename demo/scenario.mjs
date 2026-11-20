// Scripted walkthrough for the README demo. Run: node demo/record.mjs .
// The old terminal's fault switch is a demo-only endpoint on the fake terminal (port 5207), so it is flipped from here.
const fault = mode => fetch(`http://localhost:5207/TERM/FAULT/${mode}`, { method: "PUT" });

export default async ({ page, step, shot }) => {
  const get = async (path, settleMs = 500) => {
    await page.getByRole("button", { name: `GET ${path}`, exact: true }).click();
    await page.getByRole("button", { name: "Send", exact: true }).waitFor();
    await page.waitForTimeout(settleMs);
  };
  const customers = page.getByRole("group", { name: "customers mode" });

  await step("Janus sits in front of the old terminal: today every request is served by legacy", async () => {
    await get("/customers/10042");
    await get("/schedules/LN-200310");
  });
  await shot("legacy");
  await step("Shadow, switched at runtime: legacy still answers while the new module is compared", async () => {
    await customers.getByRole("button", { name: "Shadow" }).click();
    const paths = ["/customers", "/customers/10042", "/customers/99999"];
    const card = page.getByRole("region", { name: "Capabilities" });
    // The console says "Ready to flip" after 10 shadow comparisons with no mismatch.
    for (let i = 0; i < 16 && !(await card.getByText("Ready to flip").count()); i++) await get(paths[i % paths.length], 250);
    await card.getByText("Ready to flip").waitFor();
    await page.waitForTimeout(600);
  });
  await shot("shadow");
  await step("Ten comparisons, no differences: ready to flip to New, and the new module now serves it", async () => {
    await customers.getByRole("button", { name: "New" }).click();
    await page.waitForTimeout(600);
    await get("/customers/10042");
  });
  await step("The old terminal goes down: an unmigrated call fails fast with a clean 503", async () => {
    await fault("ERROR");
    await get("/schedules/LN-200310");
    await page.waitForTimeout(800);
  });
  await shot("outage");
  await step("Migrated customers keep working through the outage, served by new", async () => {
    await get("/customers/10042");
    await page.waitForTimeout(800);
  });
  await fault("NONE");
};

export async function mobile({ page, shot }) {
  await page.getByRole("group", { name: "customers mode" }).waitFor();
  await page.getByRole("button", { name: "GET /customers/10042", exact: true }).click();
  await page.getByRole("table").waitFor();
  await shot("mobile");
}
