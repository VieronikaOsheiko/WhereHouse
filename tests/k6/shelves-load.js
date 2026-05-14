/**
 * Навантажувальне тестування GET /api/shelves.
 * Запуск (локально): BASE_URL=http://localhost:5243 k6 run tests/k6/shelves-load.js
 */
import http from "k6/http";
import { check, sleep } from "k6";

const baseUrl = __ENV.BASE_URL || "http://localhost:5243";

export const options = {
  stages: [
    { duration: "15s", target: 15 },
    { duration: "30s", target: 25 },
    { duration: "10s", target: 0 },
  ],
  thresholds: {
    http_req_duration: ["p(95)<2000"],
    http_req_failed: ["rate<0.05"],
  },
};

export default function () {
  const res = http.get(`${baseUrl}/api/shelves`);
  check(res, { "shelves 200": (r) => r.status === 200 });
  sleep(0.05);
}
