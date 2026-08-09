import argparse
import json
import re
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


ALLOWED_FIELDS = {
    "schemaVersion",
    "eventId",
    "generatedUtc",
    "sessionHash",
    "productName",
    "version",
    "platform",
    "scriptingBackend",
    "eventName",
    "checkpoint",
    "category",
    "fingerprint",
    "levelIndex",
    "levelId",
    "mapId",
    "wave",
    "actualSeconds",
    "averageFps",
    "p95FrameMilliseconds",
    "reservedMemoryMegabytes",
    "cloudMatrixRows",
    "passed",
}
FORBIDDEN_PATTERNS = (
    r"sk-proj-",
    r"@",
    r"c:\\users\\",
    r"/users/",
    r"192\.0\.2\.1",
    r"api[_-]?key",
    r"bearer\s",
    r"access[_-]?token",
)


class CaptureState:
    def __init__(self, capture_path: Path, summary_path: Path, fail_first: int):
        self.capture_path = capture_path
        self.summary_path = summary_path
        self.fail_first = fail_first
        self.lock = threading.Lock()
        self.requests = 0
        self.accepted = 0
        self.planned_failures = 0
        self.invalid = 0
        self.forbidden_matches = []
        self.unexpected_fields = []

    def record(self, payload, valid, forbidden, unexpected):
        with self.lock:
            self.requests += 1
            status = 422
            if valid and self.planned_failures < self.fail_first:
                self.planned_failures += 1
                status = 503
            elif valid:
                self.accepted += 1
                status = 204
            else:
                self.invalid += 1

            self.forbidden_matches.extend(forbidden)
            self.unexpected_fields.extend(unexpected)
            record = {
                "requestIndex": self.requests,
                "responseStatus": status,
                "valid": valid,
                "forbiddenMatches": forbidden,
                "unexpectedFields": unexpected,
                "payload": payload,
            }
            self.capture_path.parent.mkdir(parents=True, exist_ok=True)
            with self.capture_path.open("a", encoding="utf-8") as stream:
                stream.write(json.dumps(record, ensure_ascii=True, separators=(",", ":")) + "\n")
            self.write_summary()
            return status

    def write_summary(self):
        summary = {
            "schemaVersion": "p1254-mock-telemetry-summary-v1",
            "requests": self.requests,
            "accepted": self.accepted,
            "plannedFailures": self.planned_failures,
            "invalid": self.invalid,
            "forbiddenMatches": sorted(set(self.forbidden_matches)),
            "unexpectedFields": sorted(set(self.unexpected_fields)),
            "passed": (
                self.requests >= self.fail_first + 2
                and self.accepted >= 2
                and self.planned_failures == self.fail_first
                and self.invalid == 0
                and not self.forbidden_matches
                and not self.unexpected_fields
            ),
        }
        self.summary_path.parent.mkdir(parents=True, exist_ok=True)
        temp_path = self.summary_path.with_suffix(self.summary_path.suffix + ".tmp")
        temp_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
        temp_path.replace(self.summary_path)


class Handler(BaseHTTPRequestHandler):
    server_version = "EmberlineP1254Mock/1.0"

    def do_GET(self):
        if self.path != "/health":
            self.send_error(404)
            return
        body = b'{"status":"ok"}'
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self):
        if self.path != "/v1/events":
            self.send_error(404)
            return

        try:
            length = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            self.send_error(400)
            return
        if length <= 0 or length > 1024 * 1024:
            self.send_error(413)
            return

        raw = self.rfile.read(length)
        try:
            payload = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            payload = {}

        unexpected = sorted(set(payload.keys()) - ALLOWED_FIELDS) if isinstance(payload, dict) else ["<non-object>"]
        serialized = json.dumps(payload, ensure_ascii=True).lower()
        forbidden = [
            pattern
            for pattern in FORBIDDEN_PATTERNS
            if re.search(pattern, serialized, flags=re.IGNORECASE)
        ]
        valid = (
            isinstance(payload, dict)
            and payload.get("schemaVersion") == "p1254-telemetry-event-v1"
            and bool(payload.get("eventId"))
            and bool(payload.get("sessionHash"))
            and bool(payload.get("eventName"))
            and not unexpected
            and not forbidden
        )
        status = self.server.capture_state.record(payload, valid, forbidden, unexpected)
        self.send_response(status)
        self.send_header("Content-Length", "0")
        self.end_headers()

    def log_message(self, *_):
        return


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=18454)
    parser.add_argument("--capture", type=Path, required=True)
    parser.add_argument("--summary", type=Path, required=True)
    parser.add_argument("--fail-first", type=int, default=1)
    args = parser.parse_args()

    state = CaptureState(args.capture.resolve(), args.summary.resolve(), max(0, args.fail_first))
    state.capture_path.unlink(missing_ok=True)
    state.summary_path.unlink(missing_ok=True)
    state.write_summary()
    server = ThreadingHTTPServer((args.host, args.port), Handler)
    server.capture_state = state
    server.serve_forever(poll_interval=0.1)


if __name__ == "__main__":
    main()
