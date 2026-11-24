#include "ot_match.h"
#include <string.h>

static uint32_t next_revision(uint32_t revision)
{
    return revision == UINT32_MAX ? 1u : revision + 1u;
}

static void new_round(OtMatch *m)
{
    uint32_t revision = next_revision(m->game.revision);
    ot_init(&m->game);
    m->game.revision = revision;
    m->round = next_revision(m->round);
    m->phase = m->before_edit = OT_RUNNING;
    m->result = OT_RESULT_NONE;
    m->edited = 0u;
    m->computer_wait_ms = 0u;
    m->elapsed_ms = 0u;
    m->remaining_ms[0] = m->remaining_ms[1] = m->mode == OT_TIMED ? (uint32_t)m->limit_seconds * 1000u : 0u;
}

void ot_match_init(OtMatch *m)
{
    memset(m, 0, sizeof(*m));
    m->limit_seconds = OT_DEFAULT_SECONDS;
    new_round(m);
}

OtMode ot_match_active_mode(const OtMatch *m)
{
    if (m->result == OT_RESULT_WIN || m->result == OT_RESULT_OVER) return OT_CASUAL;
    return m->mode;
}

static void clear_scores(OtMatch *m)
{
    m->total = 0u;
    m->total_half_point = 0u;
    m->losses = 0u;
    m->stage = m->mode == OT_CHALLENGE ? 1u : 0u;
}

unsigned ot_match_score_half(const OtMatch *m)
{
    unsigned multiplier = m->mode == OT_CHALLENGE ? m->stage : 2u;
    if (m->phase == OT_FINISHED && (m->edited || m->result == OT_RESULT_EXIT)) return 0u;
    return m->game.black * multiplier;
}

static void finish(OtMatch *m, OtResult reason)
{
    unsigned score_half;
    m->phase = OT_FINISHED;
    m->result = reason;
    m->game.status = OT_OVER;
    m->game.turn = 0u;
    if (reason == OT_RESULT_EXIT || m->edited) return;

    score_half = m->total_half_point + ot_match_score_half(m);
    if (m->total <= UINT32_MAX - score_half / 2u) {
        m->total += score_half / 2u;
        m->total_half_point = (uint8_t)(score_half % 2u);
    } else {
        m->total = UINT32_MAX;
        m->total_half_point = 0u;
    }
    if (m->mode != OT_CHALLENGE) return;

    m->losses = m->game.black < m->game.white ? (uint8_t)(m->losses + 1u) : 0u;
    if (m->total >= OT_WIN_SCORE) m->result = OT_RESULT_WIN;
    else if (m->losses == 2u || m->stage == OT_CHALLENGE_STAGES) m->result = OT_RESULT_OVER;
}

void ot_match_set_standby(OtMatch *m, uint8_t standby)
{
    if (m->standby == standby) return;
    m->standby = standby;
    if (standby) {
        if (m->phase != OT_FINISHED) finish(m, OT_RESULT_EXIT);
        m->game.revision = next_revision(m->game.revision);
    } else {
        m->mode = OT_CASUAL;
        clear_scores(m);
        new_round(m);
    }
}

uint8_t ot_match_computer_turn(const OtMatch *m)
{
    return (uint8_t)(m->mode == OT_CHALLENGE && m->phase == OT_RUNNING && m->game.turn == OT_WHITE);
}

uint8_t ot_match_external_turn(const OtMatch *m)
{
    return (uint8_t)(m->mode == OT_EXTERNAL_AI && m->phase == OT_RUNNING && m->game.turn == OT_WHITE);
}

static uint8_t apply_move(OtMatch *m, uint8_t row, uint8_t col)
{
    if (!ot_move(&m->game, row, col)) return 0u;
    if (m->game.revision == 0u) m->game.revision = 1u;
    if (m->game.status == OT_OVER) finish(m, OT_RESULT_BOARD);
    m->computer_wait_ms = ot_match_computer_turn(m) ? OT_COMPUTER_DELAY_MS : 0u;
    return 1u;
}

static int corner(uint8_t row, uint8_t col)
{
    return (row == 0u || row == 7u) && (col == 0u || col == 7u);
}

static unsigned greedy_move(const OtGame *game, int prefer_corner)
{
    OtGame candidate;
    unsigned square, best = 64u, best_white = 0u;
    for (square = 0u; square < 64u; ++square) {
        uint8_t row = (uint8_t)(square / 8u), col = (uint8_t)(square % 8u);
        candidate = *game;
        if (!ot_move(&candidate, row, col)) continue;
        if (prefer_corner && corner(row, col)) return square;
        if (candidate.white > best_white) {
            best = square;
            best_white = candidate.white;
        }
    }
    return best;
}

/* Scores are from White's perspective. A terminal result outweighs positional terms. */
static int position_value(const OtGame *game)
{
    static const unsigned corners[] = {0u, 7u, 56u, 63u};
    static const unsigned diagonals[] = {9u, 14u, 49u, 54u};
    unsigned square, i;
    int discs = (int)game->white - game->black;
    int score = discs * (game->white + game->black < 48 ? 1 : 3);
    if (game->status == OT_OVER)
        return discs > 0 ? 1000 + discs : discs < 0 ? -1000 + discs : 0;

    for (i = 0u; i < 4u; ++i) {
        uint8_t piece = game->board[corners[i]];
        if (piece == OT_WHITE) score += 32;
        else if (piece == OT_BLACK) score -= 32;
        else if (game->board[diagonals[i]] == OT_WHITE) score -= 12;
        else if (game->board[diagonals[i]] == OT_BLACK) score += 12;
    }
    for (square = 0u; square < 64u; ++square) {
        uint8_t row = (uint8_t)(square / 8u), col = (uint8_t)(square % 8u);
        if (game->board[square] != OT_EMPTY) continue;
        if (ot_legal(game, row, col, OT_WHITE)) score += 4;
        if (ot_legal(game, row, col, OT_BLACK)) score -= 4;
    }
    return score;
}

enum { SEARCH_BOUND = 2000, SEARCH_NODES = 256 };

static int search_position(const OtGame *game, unsigned plies, int alpha, int beta,
    unsigned *nodes_left, unsigned *best_move)
{
    OtGame candidate;
    unsigned square;
    int white = game->turn == OT_WHITE;
    int best = white ? -SEARCH_BOUND : SEARCH_BOUND;
    --*nodes_left;
    if (plies == 0u || game->status == OT_OVER) return position_value(game);

    for (square = 0u; square < 64u && *nodes_left != 0u; ++square) {
        int score;
        candidate = *game;
        if (!ot_move(&candidate, (uint8_t)(square / 8u), (uint8_t)(square % 8u))) continue;
        score = search_position(&candidate, plies - 1u, alpha, beta, nodes_left, NULL);
        if (white ? score > best : score < best) {
            best = score;
            if (best_move != NULL) *best_move = square;
        }
        if (white && best > alpha) alpha = best;
        if (!white && best < beta) beta = best;
        if (alpha >= beta) break;
    }
    return best;
}

static uint8_t play_computer_move(OtMatch *m)
{
    unsigned best = greedy_move(&m->game, m->stage >= 2u);
    if (best < 64u && m->stage >= 3u) {
        unsigned depth, nodes_left = SEARCH_NODES;
        /* Keep the last completed search if the next depth exhausts the budget. */
        for (depth = 1u; depth < m->stage && nodes_left != 0u; ++depth) {
            unsigned move = best;
            (void)search_position(&m->game, depth, -SEARCH_BOUND, SEARCH_BOUND, &nodes_left, &move);
            if (nodes_left != 0u) best = move;
        }
    }
    if (best < 64u) return apply_move(m, (uint8_t)(best / 8u), (uint8_t)(best % 8u));

    m->game.revision = next_revision(m->game.revision);
    finish(m, OT_RESULT_BOARD);
    return 1u;
}

uint8_t ot_match_tick(OtMatch *m, uint32_t now)
{
    uint32_t delta = m->clock_seen ? now - m->clock_ms : 0u;
    m->clock_seen = 1u;
    m->clock_ms = now;
    if (m->phase != OT_RUNNING) return 0u;
    if (m->mode == OT_TIMED) {
        uint32_t *remaining = &m->remaining_ms[m->game.turn == OT_WHITE ? 1u : 0u];
        if (delta >= *remaining) {
            m->elapsed_ms += *remaining;
            *remaining = 0u;
            m->game.revision = next_revision(m->game.revision);
            finish(m, OT_RESULT_TIME);
            return 1u;
        }
        *remaining -= delta;
    }
    if (m->elapsed_ms <= UINT32_MAX - delta) m->elapsed_ms += delta;
    else m->elapsed_ms = UINT32_MAX;
    if (ot_match_computer_turn(m)) {
        if (delta >= m->computer_wait_ms) return play_computer_move(m);
        m->computer_wait_ms -= (uint16_t)delta;
    }
    return 0u;
}

uint8_t ot_match_move(OtMatch *m, uint8_t row, uint8_t col)
{
    if (m->phase != OT_RUNNING || ot_match_computer_turn(m) || ot_match_external_turn(m)) return 0u;
    return apply_move(m, row, col);
}

uint8_t ot_match_ai_move(OtMatch *m, uint8_t row, uint8_t col)
{
    if (!ot_match_external_turn(m)) return 0u;
    return apply_move(m, row, col);
}

uint8_t ot_match_control(OtMatch *m, OtControl action, uint32_t value, uint32_t seconds)
{
    if (m->standby) return 0u;
    switch (action) {
    case OT_START:
    case OT_RESTART:
        if (action == OT_START && m->phase != OT_FINISHED) return 0u;
        if (m->phase == OT_FINISHED) {
            m->mode = ot_match_active_mode(m);
            if (m->result == OT_RESULT_WIN || m->result == OT_RESULT_OVER || m->result == OT_RESULT_EXIT)
                clear_scores(m);
            else if (m->mode == OT_CHALLENGE && !m->edited)
                ++m->stage;
        }
        new_round(m);
        return 1u;
    case OT_SET_MODE:
        if ((value > OT_TIMED && value != OT_EXTERNAL_AI) || seconds == 0u || seconds > OT_MAX_SECONDS) return 0u;
        m->mode = (OtMode)value;
        m->limit_seconds = (uint16_t)seconds;
        clear_scores(m);
        new_round(m);
        return 1u;
    case OT_PAUSE:
        if (m->phase != OT_RUNNING) return 0u;
        m->phase = OT_PAUSED;
        break;
    case OT_RESUME:
        if (m->phase != OT_PAUSED) return 0u;
        m->phase = OT_RUNNING;
        if (!ot_has_move(&m->game, m->game.turn)) finish(m, OT_RESULT_BOARD);
        break;
    case OT_END:
        if (m->phase == OT_FINISHED) return 0u;
        finish(m, OT_RESULT_EXIT);
        break;
    case OT_TOGGLE_EDIT:
        if (m->phase == OT_FINISHED) return 0u;
        if (m->phase == OT_EDITING) {
            m->phase = m->before_edit;
            if (m->phase == OT_RUNNING && !ot_has_move(&m->game, m->game.turn))
                finish(m, OT_RESULT_BOARD);
        } else {
            m->before_edit = m->phase;
            m->phase = OT_EDITING;
        }
        break;
    default:
        return 0u;
    }
    m->game.revision = next_revision(m->game.revision);
    return 1u;
}

uint8_t ot_match_edit(OtMatch *m, uint8_t row, uint8_t col, uint8_t piece)
{
    uint8_t *cell;
    if (m->phase != OT_EDITING || row >= 8u || col >= 8u || piece > OT_WHITE) return 0u;
    cell = &m->game.board[row * 8u + col];
    if (*cell == piece) return 1u;
    m->game.black -= *cell == OT_BLACK;
    m->game.white -= *cell == OT_WHITE;
    *cell = piece;
    m->game.black += piece == OT_BLACK;
    m->game.white += piece == OT_WHITE;
    m->edited = 1u;
    m->game.revision = next_revision(m->game.revision);
    return 1u;
}
