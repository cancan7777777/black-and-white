#include "ot_protocol.h"
#include <stdlib.h>

/* The offline device uses the existing firmware rule/protocol source unchanged. */
__declspec(dllexport) void * __cdecl sim_create(void) {
    OtGame *game = (OtGame *)malloc(sizeof(OtGame));
    if (game != NULL) ot_init(game);
    return game;
}
__declspec(dllexport) void __cdecl sim_destroy(void *handle) { free(handle); }
__declspec(dllexport) int __cdecl sim_exchange(void *handle, const char *request,
                                            char *response, unsigned capacity) {
    if (handle == NULL || request == NULL || response == NULL || capacity < OT_LINE_MAX) return -1;
    return ot_command((OtGame *)handle, request, response, capacity);
}
