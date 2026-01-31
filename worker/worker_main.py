from __future__ import annotations

import argparse
import asyncio
import socket

import uvicorn


def _pick_port(host: str, port: int) -> int:
    if port != 0:
        return port
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind((host, 0))
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        return sock.getsockname()[1]


async def _run_server(host: str, port: int, log_level: str) -> None:
    shutdown_event = asyncio.Event()
    from app import app as fastapi_app

    fastapi_app.state.shutdown_event = shutdown_event
    config = uvicorn.Config(
        "app:app",
        host=host,
        port=port,
        log_level=log_level,
        access_log=False,
    )
    server = uvicorn.Server(config)

    async def _watch_shutdown() -> None:
        await shutdown_event.wait()
        server.should_exit = True

    shutdown_task = asyncio.create_task(_watch_shutdown())
    try:
        await server.serve()
    finally:
        shutdown_task.cancel()


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
    asyncio.run(_run_server(args.host, port, args.log_level))


if __name__ == "__main__":
    main()
