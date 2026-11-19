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
    m->remaining_ms = m->mode == OT_TIMED ? (uint32_t)m->limit_seconds * 1000u : 0u;
}

void ot_match_init(OtMatch *m)
{
    memset(m, 0, sizeof(*m));
    m->limit_seconds = OT_DEFAULT_SECONDS;
    new_round(m);
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

uint8_t ot_match_computer_turn(const OtMatch *m)
{
    return (uint8_t)(m->mode == OT_CHALLENGE && m->phase == OT_RUNNING && m->game.turn == OT_WHITE);
}

static uint8_t apply_move(OtMatch *m, uint8_t row, uint8_t col)
{
    if (!ot_move(&m->game, row, col)) return 0u;
    if (m->game.revision == 0u) m->game.revision = 1u;
    if (m->game.status == OT_OVER) finish(m, OT_RESULT_BOARD);
    m->computer_wait_ms = ot_match_computer_turn(m) ? OT_COMPUTER_DELAY_MS : 0u;
    return 1u;
}

static uint8_t play_computer_move(OtMatch *m)
{
    OtGame candidate;
    unsigned square, best = 64u, best_white = 0u;
    for (square = 0u; square < 64u; ++square) {
        uint8_t row = (uint8_t)(square / 8u), col = (uint8_t)(square % 8u);
        if (!ot_legal(&m->game, row, col, OT_WHITE)) continue;
        if ((row == 0u || row == 7u) && (col == 0u || col == 7u))
            return apply_move(m, row, col);

        candidate = m->game;
        (void)ot_move(&candidate, row, col);
        if (candidate.white > best_white) {
            best = square;
            best_white = candidate.white;
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
    if (m->mode == OT_TIMED && delta >= m->remaining_ms) {
        m->elapsed_ms += m->remaining_ms;
        m->remaining_ms = 0u;
        m->game.revision = next_revision(m->game.revision);
        finish(m, OT_RESULT_TIME);
        return 1u;
    }
    if (m->mode == OT_TIMED) m->remaining_ms -= delta;
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
    if (m->phase != OT_RUNNING || ot_match_computer_turn(m)) return 0u;
    return apply_move(m, row, col);
}

uint8_t ot_match_control(OtMatch *m, OtControl action, uint32_t value, uint32_t seconds)
{
    switch (action) {
    case OT_START:
    case OT_RESTART:
        if (action == OT_START && m->phase != OT_FINISHED) return 0u;
        if (m->phase == OT_FINISHED) {
            if (m->result == OT_RESULT_WIN || m->result == OT_RESULT_OVER || m->result == OT_RESULT_EXIT)
                clear_scores(m);
            else if (m->mode == OT_CHALLENGE && !m->edited)
                ++m->stage;
        }
        new_round(m);
        return 1u;
    case OT_SET_MODE:
        if (value > OT_TIMED || seconds == 0u || seconds > OT_MAX_SECONDS) return 0u;
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
