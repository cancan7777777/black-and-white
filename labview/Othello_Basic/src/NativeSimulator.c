#include "ot_protocol.h"
#include <stdlib.h>
#include <windows.h>

/* Exercise the same match and command implementations as the STM32. */
typedef struct {
    OtMatch match;
    OtStateFormat format;
} Simulator;

__declspec(dllexport) void * __cdecl sim_create(void) {
    Simulator *sim = (Simulator *)malloc(sizeof(Simulator));
    if (sim != NULL) {
        sim->format = OT_FORMAT_BASIC;
        ot_match_init(&sim->match);
        (void)ot_match_tick(&sim->match, GetTickCount());
    }
    return sim;
}
__declspec(dllexport) void __cdecl sim_destroy(void *handle) { free(handle); }
__declspec(dllexport) int __cdecl sim_exchange(void *handle, const char *request,
                                            char *response, unsigned capacity) {
    Simulator *sim = (Simulator *)handle;
    if (sim == NULL || request == NULL || response == NULL || capacity < OT_LINE_MAX) return -1;
    (void)ot_match_tick(&sim->match, GetTickCount());
    return ot_match_command(&sim->match, &sim->format, request, response, capacity);
}
