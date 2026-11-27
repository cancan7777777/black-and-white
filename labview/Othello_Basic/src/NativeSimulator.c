#include "ot_protocol.h"
#include <stdlib.h>
#include <string.h>
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

static int read_position(OtGame *game, const char *cells, int turn) {
    unsigned square;
    if (cells == NULL || strlen(cells) != 64u || (turn != OT_BLACK && turn != OT_WHITE)) return 0;
    ot_init(game);
    game->black = game->white = 0;
    game->turn = (uint8_t)turn;
    for (square = 0; square < 64u; ++square) {
        if (cells[square] < '0' || cells[square] > '2') return 0;
        game->board[square] = (uint8_t)(cells[square] - '0');
        game->black += game->board[square] == OT_BLACK;
        game->white += game->board[square] == OT_WHITE;
    }
    return 1;
}

static unsigned pick_move(const OtGame *game, uint32_t *seed) {
    unsigned count = 0, square, weight = 0, ticket;
    uint8_t moves[64], weights[64];
    for (square = 0; square < 64u; ++square) {
        unsigned row = square / 8u, col = square % 8u;
        if (ot_legal(game, (uint8_t)row, (uint8_t)col, game->turn)) {
            int corner = (row == 0u || row == 7u) && (col == 0u || col == 7u);
            moves[count] = (uint8_t)square;
            weights[count] = (uint8_t)(corner ? 8u : 1u);
            weight += weights[count++];
        }
    }
    if (weight == 0u) return 64u;
    *seed ^= *seed << 13;
    *seed ^= *seed >> 17;
    *seed ^= *seed << 5;
    ticket = *seed % weight;
    for (square = 0; ticket >= weights[square]; ++square) ticket -= weights[square];
    return moves[square];
}

__declspec(dllexport) int __cdecl sim_win_rates(const char *cells, int turn, uint32_t seed,
    unsigned games, unsigned *black, unsigned *white) {
    OtGame start;
    unsigned trial;
    if (!read_position(&start, cells, turn) || games == 0 || games > 256u ||
        !ot_has_move(&start, start.turn) || black == NULL || white == NULL) return 0;
    *black = *white = 0;
    if (seed == 0u) seed = 1u;
    for (trial = 0; trial < games; ++trial) {
        OtGame game = start;
        while (game.status == OT_PLAY) {
            unsigned square = pick_move(&game, &seed);
            if (square == 64u || !ot_move(&game, (uint8_t)(square / 8u), (uint8_t)(square % 8u))) break;
        }
        *black += game.black > game.white;
        *white += game.white > game.black;
    }
    return 1;
}

__declspec(dllexport) int __cdecl sim_recorded_move(const char *before, int turn, const char *after) {
    OtGame game;
    unsigned square, placed = 64u;
    if (!read_position(&game, before, turn) || after == NULL || strlen(after) != 64u) return -1;
    for (square = 0; square < 64u; ++square) {
        if (game.board[square] != OT_EMPTY || after[square] != '0' + turn) continue;
        if (placed != 64u) return -1;
        placed = square;
    }
    if (placed == 64u || !ot_move(&game, (uint8_t)(placed / 8u), (uint8_t)(placed % 8u))) return -1;
    for (square = 0; square < 64u; ++square)
        if (after[square] != '0' + game.board[square]) return -1;
    return (int)placed;
}
