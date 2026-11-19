#ifndef OT_MATCH_H
#define OT_MATCH_H
#include "othello.h"

#define OT_WIN_SCORE 188u
#define OT_DEFAULT_SECONDS 180u
#define OT_MAX_SECONDS 3600u

typedef enum { OT_CASUAL, OT_CHALLENGE, OT_TIMED } OtMode;
typedef enum { OT_RUNNING, OT_PAUSED, OT_EDITING, OT_FINISHED } OtPhase;
typedef enum { OT_RESULT_NONE, OT_RESULT_BOARD, OT_RESULT_TIME, OT_RESULT_EXIT,
               OT_RESULT_WIN, OT_RESULT_OVER } OtResult;
typedef enum { OT_START, OT_PAUSE, OT_RESUME, OT_END, OT_RESTART,
               OT_SET_MODE, OT_TOGGLE_EDIT } OtControl;

typedef struct {
    OtGame game;
    OtMode mode;
    OtPhase phase, before_edit;
    OtResult result;
    uint32_t round, total, remaining_ms, elapsed_ms, clock_ms;
    uint16_t limit_seconds;
    uint8_t losses, edited, clock_seen;
} OtMatch;

void ot_match_init(OtMatch *match);
/* Returns true when time expires and the round is settled. */
uint8_t ot_match_tick(OtMatch *match, uint32_t now);
uint8_t ot_match_move(OtMatch *match, uint8_t row, uint8_t col);
uint8_t ot_match_control(OtMatch *match, OtControl action, uint32_t value, uint32_t seconds);
uint8_t ot_match_edit(OtMatch *match, uint8_t row, uint8_t col, uint8_t piece);

#endif
