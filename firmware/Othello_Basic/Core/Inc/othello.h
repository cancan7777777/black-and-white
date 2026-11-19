#ifndef OTHELLO_H
#define OTHELLO_H
#include <stdint.h>
#define OT_EMPTY 0u
#define OT_BLACK 1u
#define OT_WHITE 2u
#define OT_PLAY 0u
#define OT_OVER 1u
typedef struct { uint8_t board[64], turn, status, black, white; uint32_t revision; } OtGame;
void ot_init(OtGame *g);
uint8_t ot_legal(const OtGame *g, uint8_t row, uint8_t col, uint8_t player);
uint8_t ot_has_move(const OtGame *g, uint8_t player);
uint8_t ot_move(OtGame *g, uint8_t row, uint8_t col);
#endif
