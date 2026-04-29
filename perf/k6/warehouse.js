import http from "k6/http";
import { check, sleep } from "k6";
import { Rate } from "k6/metrics";

const failRate = new Rate("failed_requests");

export const options = {
  scenarios: {
    shelves_browse: {
      executor: "ramping-vus",
      startVUs: 0,
      stages: [
        { duration: "30s", target: 30 },
        { duration: "1m", target: 60 },
        { duration: "30s", target: 0 },
      ],
      gracefulRampDown: "30s",
      exec: "browseShelves",
    },
    mixed_read: {
      executor: "constant-vus",
      vus: 20,
      duration: "2m",
      startTime: "30s",
      exec: "mixedRead",
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.05"],
    http_req_duration: ["p(95)<2000"],
    failed_requests: ["rate<0.05"],
  },
};

const base = __ENV.BASE_URL || "http://localhost:5243";

export function browseShelves() {
  const res = http.get(`${base}/api/shelves?minAvailableCapacity=0`);
  failRate.add(res.status !== 200);
  check(res, {
    "shelves status 200": (r) => r.status === 200,
  });
  sleep(0.2);
}

export function mixedRead() {
  const zones = http.get(`${base}/api/zones`);
  failRate.add(zones.status !== 200);
  check(zones, { "zones 200": (r) => r.status === 200 });

  const shelves = http.get(`${base}/api/shelves`);
  failRate.add(shelves.status !== 200);
  check(shelves, { "shelves list 200": (r) => r.status === 200 });

  sleep(0.15);
}
