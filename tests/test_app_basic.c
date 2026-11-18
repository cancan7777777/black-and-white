#include "app_basic.h"
#include "ot_protocol.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>

static char sent[OT_LINE_MAX];
static unsigned ready = 1u, renders, observed[4], reentry_rejections;
static uint8_t can_send(void *p) { (void)p; return (uint8_t)ready; }
static uint8_t send_frame(const char *s, size_t n, void *p)
{ (void)p; assert(n < sizeof(sent)); memcpy(sent, s, n); sent[n] = 0; return 1u; }
static void render(const AppBasicSnapshot *s, uint8_t blink, void *p)
{ (void)p; (void)blink; assert(s->cursor_row < 8u && s->cursor_col < 8u); ++renders; }
static void observer(const AppBasicSnapshot *s, AppBasicSource source, void *context)
{
    unsigned slot = *(unsigned *)context;
    (void)source;
    ++observed[slot];
    if (AppBasic_RequestMove(0u, 0u, s->game.revision, APP_SOURCE_EXTENSION) == APP_MOVE_REENTRANT)
        ++reentry_rejections;
    AppBasic_HandleKey('0'); /* Reentrant mutations must have no effect. */
    AppBasic_Init(0);
}
static void frame(const char *body, char *out)
{ (void)snprintf(out, OT_LINE_MAX, "%s,%04X\n", body, (unsigned)ot_crc(body, strlen(body))); }
int main(void)
{
    AppBasicPorts ports = { can_send, send_frame, render, 0 };
    AppBasicSnapshot before, after;
    unsigned contexts[4] = { 0u, 1u, 2u, 3u }, i, moves;
    int slots[4];
    char input[OT_LINE_MAX], body[96];
    AppBasic_Init(&ports);
    AppBasic_GetSnapshot(&before);
    assert(before.game.black == 2u && before.game.white == 2u);
    assert(before.api_version == APP_BASIC_API_VERSION);
    before.game.board[0] = 99u;
    AppBasic_GetSnapshot(&after);
    assert(after.game.board[0] == OT_EMPTY); /* Snapshot changes cannot mutate authority. */
    AppBasic_Tick(0u);
    assert(renders == 1u && strstr(sent, "@STATE,0,1,1,0,2,2,"));
    for (i = 0u; i < 4u; ++i) { slots[i] = AppBasic_AddObserver(observer, &contexts[i]); assert(slots[i] >= 0); }
    assert(AppBasic_AddObserver(observer, &contexts[0]) == -1);
    frame("@MOVE,1,1,2,3", input);
    ready = 0u;
    assert(!AppBasic_ProcessFrame(input));
    AppBasic_GetSnapshot(&before);
    assert(before.game.revision == 1u);
    ready = 1u;
    assert(AppBasic_ProcessFrame(input));
    AppBasic_GetSnapshot(&after);
    assert(after.game.revision == 2u && after.game.black == 4u && after.game.white == 1u);
    assert(reentry_rejections == 4u && observed[0] == 1u && observed[3] == 1u);
    assert(AppBasic_ProcessFrame(input)); /* Duplicate is stale, never a second move. */
    assert(strstr(sent, ",STALE,"));
    assert(AppBasic_RequestMove(0u, 0u, 1u, APP_SOURCE_EXTENSION) == APP_MOVE_STALE);
    assert(AppBasic_ProcessFrame("@GET,2,0000\n"));
    assert(strstr(sent, ",CRC,"));
    frame("@X_AI,2", input);
    assert(AppBasic_ProcessFrame(input) && strstr(sent, ",COMMAND,"));
    AppBasic_HandleKey('0');
    AppBasic_GetSnapshot(&before);
    assert(before.game.revision == 3u && before.game.black == 2u);
    assert(AppBasic_RemoveObserver((unsigned)slots[0]));
    i = observed[0];
    assert(AppBasic_RequestMove(2u, 3u, before.game.revision, APP_SOURCE_EXTENSION) == APP_MOVE_OK);
    assert(observed[0] == i && observed[1] > i);
    for (moves = 0u; moves < 64u; ++moves) {
        AppBasic_GetSnapshot(&before);
        if (before.game.status == OT_OVER) break;
        for (i = 0u; i < 64u; ++i)
            if (ot_legal(&before.game, (uint8_t)(i / 8u), (uint8_t)(i % 8u), before.game.turn)) break;
        assert(i < 64u);
        assert(AppBasic_RequestMove((uint8_t)(i / 8u), (uint8_t)(i % 8u),
                                   before.game.revision, APP_SOURCE_EXTENSION) == APP_MOVE_OK);
    }
    AppBasic_GetSnapshot(&after);
    assert(after.game.status == OT_OVER && after.game.turn == 0u && moves < 64u);
    (void)snprintf(body, sizeof(body), "@MOVE,55,%lu,0,0", (unsigned long)after.game.revision);
    frame(body, input);
    assert(AppBasic_ProcessFrame(input) && strstr(sent, ",ILLEGAL,"));
    AppBasic_Tick(0xffffffffu);
    AppBasic_Tick(5u);
    assert(renders >= 2u);
    puts("PASS: opening, snapshots, backpressure, protocol, stale/CRC/unknown commands, reset, observers, reentry, full game, tick wrap");
    return 0;
}
