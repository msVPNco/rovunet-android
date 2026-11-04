/* Veyra: supervise the core without depending on the lifetime of a JVM worker
 * thread. Linux PDEATHSIG alone would fire if the spawning Java thread exits. */
#include <sys/prctl.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <signal.h>
#include <errno.h>
#include <unistd.h>
static volatile sig_atomic_t stopping;
static void stop(int sig) { (void)sig; stopping = 1; }
int main(int argc, char **argv) {
    if (argc < 2) return 64;
    pid_t app = getppid();
    if (app == 1) return 70;
    signal(SIGTERM, stop); signal(SIGINT, stop);
    pid_t owner = getpid(), child = fork();
    if (child < 0) return 71;
    if (child == 0) {
        signal(SIGTERM, SIG_DFL); signal(SIGINT, SIG_DFL);
        if (prctl(PR_SET_PDEATHSIG, SIGKILL) != 0 || getppid() != owner) _exit(70);
        execv(argv[1], argv + 1); _exit(71);
    }
    int status = 0;
    while (!stopping && getppid() == app) {
        pid_t done = waitpid(child, &status, WNOHANG);
        if (done == child) return WIFEXITED(status) ? WEXITSTATUS(status) : 128 + WTERMSIG(status);
        if (done < 0 && errno != EINTR) return 72;
        usleep(100000);
    }
    kill(child, SIGTERM);
    for (int i = 0; i < 3; i++) {
        if (waitpid(child, &status, WNOHANG) == child) return 0;
        usleep(100000);
    }
    kill(child, SIGKILL);
    while (waitpid(child, &status, 0) < 0 && errno == EINTR) {}
    return 0;
}
