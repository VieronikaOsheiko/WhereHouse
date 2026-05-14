/**
 * Стрес одночасного створення замовлень і переведення у Shipped.
 * У setup підтягуються товари з /api/items/expiring; якщо їх немає (чиста dev-база),
 * створюються тимчасові позиції через POST /api/items.
 * Запуск: BASE_URL=http://localhost:5243 k6 run tests/k6/orders-concurrent.js
 */
import http from "k6/http";
import { check } from "k6";

const baseUrl = __ENV.BASE_URL || "http://localhost:5243";

export const options = {
  scenarios: {
    orders: {
      executor: "constant-vus",
      vus: 25,
      duration: "45s",
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.85"],
    http_reqs: ["count>50"],
  },
};

function futureDateOnly(daysAhead) {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() + daysAhead);
  return d.toISOString().slice(0, 10);
}

function collectExpiringItemIds() {
  const res = http.get(`${baseUrl}/api/items/expiring?days=5000`);
  if (res.status !== 200) {
    return [];
  }
  const items = res.json();
  if (!Array.isArray(items) || items.length === 0) {
    return [];
  }
  const cap = Math.min(items.length, 500);
  const ids = [];
  for (let i = 0; i < cap; i++) {
    ids.push(items[i].id);
  }
  return ids;
}

function seedItemsForStress() {
  const zonesRes = http.get(`${baseUrl}/api/zones`);
  if (zonesRes.status !== 200) {
    return [];
  }
  const zones = zonesRes.json();
  const ambient = zones.find((z) => z.temperature === "Ambient");
  if (!ambient) {
    return [];
  }
  const shelvesRes = http.get(`${baseUrl}/api/shelves?zoneId=${ambient.id}`);
  if (shelvesRes.status !== 200) {
    return [];
  }
  const shelves = shelvesRes.json();
  if (!Array.isArray(shelves) || shelves.length === 0) {
    return [];
  }
  const shelfId = shelves[0].id;
  const headers = { "Content-Type": "application/json" };
  const ids = [];
  for (let i = 0; i < 150; i++) {
    const sku = `k6-${Date.now()}-${i}`;
    const body = JSON.stringify({
      name: "k6-stock",
      sku,
      weight: 0.1,
      requiredTemperature: "Ambient",
      shelfId,
      quantity: 3,
      expiryDate: futureDateOnly(60 + (i % 30)),
    });
    const r = http.post(`${baseUrl}/api/items`, body, { headers });
    if (r.status === 201) {
      const row = r.json();
      if (row && row.id) {
        ids.push(row.id);
      }
    }
  }
  return ids;
}

export function setup() {
  let itemIds = collectExpiringItemIds();
  if (itemIds.length < 20) {
    itemIds = seedItemsForStress();
  }
  return { itemIds };
}

export default function (data) {
  if (!data.itemIds || data.itemIds.length === 0) {
    return;
  }
  const id = data.itemIds[Math.floor(Math.random() * data.itemIds.length)];
  const headers = { "Content-Type": "application/json" };
  const create = http.post(
    `${baseUrl}/api/orders`,
    JSON.stringify({ lines: [{ itemId: id, quantity: 1 }] }),
    { headers },
  );
  if (create.status !== 201) {
    return;
  }
  const body = create.json();
  if (!body || !body.id) {
    return;
  }
  const patch = http.patch(
    `${baseUrl}/api/orders/${body.id}/status`,
    JSON.stringify({ status: "Shipped" }),
    { headers },
  );
  check(patch, {
    "ship 200 or 409": (r) => r.status === 200 || r.status === 409,
  });
}
