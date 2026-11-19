#include "app_basic.h"
#include "ot_protocol.h"
#include <string.h>

static struct {
    OtGame game;
    AppBasicPorts ports;
    uint8_t row, col, dirty, broadcast, blink, notifying;
    uint32_t blink_at;
    struct { AppBasicObserver callback; void *context; } observers[APP_BASIC_MAX_OBSERVERS];
} app;

void AppBasic_GetSnapshot(AppBasicSnapshot *out)
{
    if (!out) return;
    out->api_version = APP_BASIC_API_VERSION;
    out->game = app.game;
    out->cursor_row = app.row;
    out->cursor_col = app.col;
}

static void notify(AppBasicSource source)
{
    AppBasicSnapshot snapshot;
    unsigned i;
    AppBasic_GetSnapshot(&snapshot);
    app.notifying = 1u;
    for (i = 0u; i < APP_BASIC_MAX_OBSERVERS; ++i)
        if (app.observers[i].callback)
            app.observers[i].callback(&snapshot, source, app.observers[i].context);
    app.notifying = 0u;
}

void AppBasic_Init(const AppBasicPorts *ports)
{
    if (app.notifying) return;
    memset(&app, 0, sizeof(app));
    if (ports) app.ports = *ports;
    ot_init(&app.game);
    app.row = 2u;
    app.col = 3u;
    app.dirty = app.broadcast = app.blink = 1u;
}

int AppBasic_AddObserver(AppBasicObserver callback, void *context)
{
    unsigned i;
    if (!callback || app.notifying) return -1;
    for (i = 0u; i < APP_BASIC_MAX_OBSERVERS; ++i) {
        if (!app.observers[i].callback) {
            app.observers[i].callback = callback;
            app.observers[i].context = context;
            return (int)i;
        }
    }
    return -1;
}

uint8_t AppBasic_RemoveObserver(unsigned slot)
{
    if (slot >= APP_BASIC_MAX_OBSERVERS || app.notifying) return 0u;
    app.observers[slot].callback = 0;
    app.observers[slot].context = 0;
    return 1u;
}

AppBasicMoveResult AppBasic_RequestMove(uint8_t row, uint8_t col,
                                      uint32_t revision, AppBasicSource source)
{
    if (app.notifying) return APP_MOVE_REENTRANT;
    if (revision != app.game.revision) return APP_MOVE_STALE;
    if (!ot_move(&app.game, row, col)) return APP_MOVE_ILLEGAL;
    app.dirty = app.broadcast = 1u;
    notify(source);
    return APP_MOVE_OK;
}

void AppBasic_HandleKey(char key)
{
    uint8_t old_row = app.row, old_col = app.col;
    uint32_t revision;
    if (app.notifying) return;
    /* Once the game is over only the reset key is honoured, so a finished game cannot be
       nudged back into play by the cursor keys. */
    if (app.game.status != OT_PLAY && key != '0') return;
    switch (key) {
    case '2': if (app.row) --app.row; break;
    case '8': if (app.row < 7u) ++app.row; break;
    case '4': if (app.col) --app.col; break;
    case '6': if (app.col < 7u) ++app.col; break;
    case '5':
        (void)AppBasic_RequestMove(app.row, app.col, app.game.revision, APP_SOURCE_KEYPAD);
        break;
    case '0':
        revision = app.game.revision + 1u;
        ot_init(&app.game);
        /* Do not recycle revision=1 on a local reset while old requests may exist. */
        app.game.revision = revision ? revision : 1u;
        app.row = 2u; app.col = 3u;
        app.dirty = app.broadcast = 1u;
        notify(APP_SOURCE_RESET);
        break;
    default: break; /* Other keys are reserved for explicitly specified features. */
    }
    if (old_row != app.row || old_col != app.col) app.dirty = 1u;
}

uint8_t AppBasic_ProcessFrame(const char *frame)
{
    char response[OT_LINE_MAX];
    uint32_t before;
    int length;
    if (!frame || app.notifying || !app.ports.can_send || !app.ports.send ||
        !app.ports.can_send(app.ports.context)) return 0u;
    before = app.game.revision;
    length = ot_command(&app.game, frame, response, sizeof(response));
    if (length > 0 && !app.ports.send(response, (size_t)length, app.ports.context))
        app.broadcast = 1u; /* Reconcile by GET if a transport error loses a response. */
    if (before != app.game.revision) {
        app.dirty = 1u;
        notify(APP_SOURCE_SERIAL);
    }
    return 1u;
}

void AppBasic_Tick(uint32_t now)
{
    AppBasicSnapshot snapshot;
    char response[OT_LINE_MAX];
    int length;
    if (app.notifying) return;
    if ((uint32_t)(now - app.blink_at) >= 300u) {
        app.blink_at = now;
        app.blink ^= 1u;
        app.dirty = 1u;
    }
    if (app.dirty && app.ports.render) {
        AppBasic_GetSnapshot(&snapshot);
        app.ports.render(&snapshot, app.blink, app.ports.context);
        app.dirty = 0u;
    }
    if (app.broadcast && app.ports.can_send && app.ports.send &&
        app.ports.can_send(app.ports.context)) {
        length = ot_state(&app.game, 0u, response, sizeof(response));
        if (length > 0 && app.ports.send(response, (size_t)length, app.ports.context))
            app.broadcast = 0u;
    }
}
