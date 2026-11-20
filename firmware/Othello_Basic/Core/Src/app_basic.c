#include "app_basic.h"
#include "ot_protocol.h"
#include <string.h>

static struct {
    OtMatch match;
    AppBasicPorts ports;
    uint8_t row, col, dirty, broadcast, blink, notifying;
    OtStateFormat format;
    uint32_t blink_at;
    struct { AppBasicObserver callback; void *context; } observers[APP_BASIC_MAX_OBSERVERS];
} app;

void AppBasic_GetSnapshot(AppBasicSnapshot *out)
{
    if (!out) return;
    out->api_version = APP_BASIC_API_VERSION;
    out->game = app.match.game;
    out->cursor_row = app.row;
    out->cursor_col = app.col;
    out->result = app.match.result;
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
    ot_match_init(&app.match);
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
    if (revision != app.match.game.revision) return APP_MOVE_STALE;
    if (!ot_match_move(&app.match, row, col)) return APP_MOVE_ILLEGAL;
    app.dirty = app.broadcast = 1u;
    notify(source);
    return APP_MOVE_OK;
}

static void toggle_mode(OtMode mode)
{
    OtMode next = ot_match_active_mode(&app.match) == mode ? OT_CASUAL : mode;
    (void)ot_match_control(&app.match, OT_SET_MODE, next, app.match.limit_seconds);
}

void AppBasic_HandleKey(char key)
{
    uint8_t old_row = app.row, old_col = app.col;
    uint32_t revision = app.match.game.revision;
    uint32_t round = app.match.round;
    if (app.notifying) return;
    switch (key) {
    case '2': if (app.row) --app.row; break;
    case '8': if (app.row < 7u) ++app.row; break;
    case '4': if (app.col) --app.col; break;
    case '6': if (app.col < 7u) ++app.col; break;
    case '5':
        if (app.match.phase == OT_EDITING) {
            uint8_t current = app.match.game.board[8u * app.row + app.col];
            (void)ot_match_edit(&app.match, app.row, app.col, (uint8_t)((current + 1u) % 3u));
        } else {
            (void)AppBasic_RequestMove(app.row, app.col, revision, APP_SOURCE_KEYPAD);
            return;
        }
        break;
    case 'A': (void)ot_match_control(&app.match, OT_START, 0u, 0u); break;
    case 'B':
        (void)ot_match_control(&app.match, app.match.phase == OT_PAUSED ? OT_RESUME : OT_PAUSE, 0u, 0u);
        break;
    case 'C': (void)ot_match_control(&app.match, OT_END, 0u, 0u); break;
    case 'D': toggle_mode(OT_CHALLENGE); break;
    case '#': toggle_mode(OT_EXTERNAL_AI); break;
    case '9': toggle_mode(OT_TIMED); break;
    case '0': (void)ot_match_control(&app.match, OT_RESTART, 0u, 0u); break;
    case '*': (void)ot_match_control(&app.match, OT_TOGGLE_EDIT, 0u, 0u); break;
    default: break;
    }
    if (app.match.game.revision != revision) {
        if (app.match.round != round) { app.row = 2u; app.col = 3u; }
        app.dirty = app.broadcast = 1u;
        notify(app.match.round != round ? APP_SOURCE_RESET : APP_SOURCE_KEYPAD);
    }
    if (old_row != app.row || old_col != app.col) app.dirty = 1u;
}

uint8_t AppBasic_ProcessFrame(const char *frame)
{
    char response[OT_LINE_MAX];
    uint32_t before, round;
    int length;
    if (!frame || app.notifying || !app.ports.can_send || !app.ports.send ||
        !app.ports.can_send(app.ports.context)) return 0u;
    before = app.match.game.revision;
    round = app.match.round;
    length = ot_match_command(&app.match, &app.format, frame, response, sizeof(response));
    if (length > 0 && !app.ports.send(response, (size_t)length, app.ports.context)) app.broadcast = 1u;
    if (before != app.match.game.revision) {
        if (app.match.round != round) { app.row = 2u; app.col = 3u; }
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
    if (ot_match_tick(&app.match, now)) {
        app.dirty = app.broadcast = 1u;
        notify(APP_SOURCE_EXTENSION);
    }
    if ((uint32_t)(now - app.blink_at) >= 300u) {
        app.blink_at = now;
        app.blink ^= 1u;
        app.dirty = 1u;
    }
    if (app.dirty && app.ports.render) {
        AppBasic_GetSnapshot(&snapshot);
        app.ports.render(&snapshot, (uint8_t)(app.blink && app.match.phase != OT_FINISHED), app.ports.context);
        app.dirty = 0u;
    }
    if (app.broadcast && app.ports.can_send && app.ports.send &&
        app.ports.can_send(app.ports.context)) {
        length = ot_match_state(&app.match, app.format, 0u, response, sizeof(response));
        if (length > 0 && app.ports.send(response, (size_t)length, app.ports.context))
            app.broadcast = 0u;
    }
}
