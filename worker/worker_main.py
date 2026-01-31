from __future__ import annotations

import argparse
import socket

import uvicorn


def _pick_port(host: str, port: int) -> int:
    if port != 0:
        return port
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind((host, 0))
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        return sock.getsockname()[1]


def main() -> None:
    parser = argparse.ArgumentParser(description="OpenVoiceLab worker entrypoint")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=0)
    parser.add_argument("--log-level", default="info")
    args = parser.parse_args()

    if args.host in {"0.0.0.0", "::"}:
        raise ValueError("Worker must bind to 127.0.0.1 only.")

    port = _pick_port(args.host, args.port)
    print(f"WORKER_PORT={port}", flush=True)
    uvicorn.run(
        "app:app",
        host=args.host,
        port=port,
        log_level=args.log_level,
        access_log=False,
    )


if __name__ == "__main__":
    main()
