#include "ot_protocol.h"
#include <stdlib.h>
#include <windows.h>

/* Exercise the same match and command implementations as the STM32. */
__declspec(dllexport) void * __cdecl sim_create(void) {
    OtMatch *match = (OtMatch *)malloc(sizeof(OtMatch));
    if (match != NULL) {
        ot_match_init(match);
        (void)ot_match_tick(match, GetTickCount());
    }
    return match;
}
__declspec(dllexport) void __cdecl sim_destroy(void *handle) { free(handle); }
__declspec(dllexport) int __cdecl sim_exchange(void *handle, const char *request,
                                            char *response, unsigned capacity) {
    if (handle == NULL || request == NULL || response == NULL || capacity < OT_LINE_MAX) return -1;
    (void)ot_match_tick((OtMatch *)handle, GetTickCount());
    return ot_match_command((OtMatch *)handle, request, response, capacity);
}
