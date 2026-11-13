# REVIEW09 tools3

Native network binaries remain pilot17 (640fa1f). Consumer preview.9 remains the frozen 75aa82d build.
The tools2 post-lock test reported late helper launch unexpectedly succeeded.
Removal now denies Execute on each owned image, rechecks every helper after the barrier,
and restores original access rules before restarting the service on a pre-removal failure.
File sharing alone is not a cached-image execution barrier.
A host-local copied executable test verified cached launch denial (Win32 error 5), preservation of
the running process, byte preservation, ACL access-rule rollback, and successful later execution.
That test does not certify SCM removal/reinstall on tools3. The physical-host refusal remains
in the lab installer/remover; owner-host component testing did not install this service.
SYSTEM / Local Service DPAPI read-grant behavior is separately characterized and remains open.
