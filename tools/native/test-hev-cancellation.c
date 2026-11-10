/* Deterministic cancellation at the actual production I/O yield boundary. */
#include <assert.h>
#include <hev-task.h>
#include "hev-socks5.h"
#include "hev-socks5-logger-priv.h"
static HevSocks5 *current;
static int terminate_during_yield;
void hev_task_yield(HevTaskYieldType type) { if (terminate_during_yield) current->terminated = 1; }
unsigned int hev_task_sleep(unsigned int timeout) {
    if (terminate_during_yield) current->terminated = 1;
    return timeout > 0 ? timeout - 1 : 0; /* woken before timeout, not a timeout */
}
void hev_socks5_logger_log(HevSocks5LoggerLevel level, const char *fmt, ...) {}
int hev_socks5_task_io_yielder(HevTaskYieldType type, void *data);
int main(void) {
    HevSocks5 socket = {0}; current = &socket;
    socket.timeout = 120000;
    assert(hev_socks5_task_io_yielder(HEV_TASK_WAITIO, &socket) == 0);
    socket.terminated = 1;
    /* A later handshake timeout reset must never re-enable cancelled I/O. */
    socket.timeout = 120000;
    assert(hev_socks5_task_io_yielder(HEV_TASK_WAITIO, &socket) == -1);
    assert(hev_socks5_task_io_yielder(HEV_TASK_YIELD, &socket) == -1);
    socket.terminated = 0; terminate_during_yield = 1;
    assert(hev_socks5_task_io_yielder(HEV_TASK_WAITIO, &socket) == -1);
    socket.terminated = 0;
    assert(hev_socks5_task_io_yielder(HEV_TASK_YIELD, &socket) == -1);
    return 0;
}
