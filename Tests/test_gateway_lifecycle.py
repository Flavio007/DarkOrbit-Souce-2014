"""Run against a built server directory containing GatewayLifecycleHost.exe."""
import base64
import os
from pathlib import Path
import queue
import socket
import subprocess
import sys
import threading
import time

assembly = Path(sys.argv[1]).resolve()
host = assembly.with_name('GatewayLifecycleHost.exe')
with socket.socket() as reservation:
    reservation.bind(('127.0.0.1', 0))
    port = reservation.getsockname()[1]
tcp = socket.socket()
tcp.bind(('127.0.0.1', 0))
tcp.listen()
def accept():
    while True:
        try:
            connection, _ = tcp.accept()
            connection.close()
        except OSError:
            return
threading.Thread(target=accept, daemon=True).start()
env = dict(os.environ, DO_WS_PORT=str(port), DO_TCP_PORT=str(tcp.getsockname()[1]))
processes = []
def launch():
    process = subprocess.Popen([str(host), str(assembly)], env=env, stdin=subprocess.PIPE,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    process.lines = queue.Queue()
    def read():
        for line in process.stdout:
            process.lines.put(line.strip())
    threading.Thread(target=read, daemon=True).start()
    processes.append(process)
    return process
def command(process, text):
    process.stdin.write(text + '\n')
    process.stdin.flush()
def child(process):
    command(process, 'pid')
    end = time.monotonic() + 3
    while time.monotonic() < end:
        line = process.lines.get(timeout=3)
        if line.startswith('CHILD:'):
            return int(line.split(':')[1])
    raise AssertionError('host did not reply')
def listening():
    try:
        with socket.create_connection(('127.0.0.1', port), timeout=.3):
            return True
    except OSError:
        return False
def wait(predicate):
    end = time.monotonic() + 12
    while time.monotonic() < end:
        if predicate():
            return
        time.sleep(.15)
    raise AssertionError('lifecycle condition timed out')
def check(condition, label):
    assert condition, label
    print('PASS', label, flush=True)
try:
    first = launch()
    wait(listening)
    first_pid = child(first)
    check(first_pid > 0, 'server startup launches gateway automatically')
    with socket.create_connection(('127.0.0.1', port), timeout=2) as ws:
        key = base64.b64encode(os.urandom(16)).decode()
        ws.sendall((f'GET /game HTTP/1.1\r\nHost: localhost:{port}\r\nUpgrade: websocket\r\n'
            f'Connection: Upgrade\r\nSec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n'
            'Origin: http://localhost\r\n\r\n').encode())
        check(b'101 Switching Protocols' in ws.recv(4096), 'automatically started gateway accepts WebSocket handshake')
    second = launch()
    time.sleep(.7)
    check(child(second) == 0, 'existing gateway is reused without duplicate child')
    command(second, 'stop')
    second.wait(timeout=5)
    check(listening(), 'stopping non-owner preserves existing gateway')
    command(first, 'kill')
    wait(lambda: (new_pid := child(first)) > 0 and new_pid != first_pid)
    wait(listening)
    check(True, 'gateway restarts automatically after child crash')
    command(first, 'stop')
    first.wait(timeout=5)
    wait(lambda: not listening())
    check(True, 'normal server shutdown stops owned gateway')
    third = launch()
    wait(listening)
    third.kill()
    third.wait(timeout=5)
    wait(lambda: not listening())
    check(True, 'forced server termination does not leave orphan gateway')
finally:
    for process in processes:
        if process.poll() is None:
            command(process, 'stop')
            process.wait(timeout=5)
    tcp.close()
