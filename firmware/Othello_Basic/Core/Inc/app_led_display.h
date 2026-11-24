#ifndef APP_LED_DISPLAY_H
#define APP_LED_DISPLAY_H

#include "app_config.h"
#include "othello.h"

#define APP_LED_WELCOME_STEPS 28u
#define APP_LED_WELCOME_STEP_MS 90u

static inline uint32_t app_led_piece_color(uint8_t piece, uint8_t level)
{
    if (piece == OT_BLACK) return ((uint32_t)(level / 4u) << 16) | ((uint32_t)level << 8);
    if (piece == OT_WHITE) return ((uint32_t)level << 16) | level;
    return 0u;
}

static inline uint32_t app_led_board_pixel(uint8_t piece, uint8_t selected, uint8_t turn)
{
    if (piece == OT_EMPTY && selected) return app_led_piece_color(turn, APP_LED_PREVIEW_LEVEL);
    return app_led_piece_color(piece, selected ? APP_LED_CURSOR_LEVEL : APP_LED_PIECE_LEVEL);
}

static inline uint32_t app_led_welcome_pixel(unsigned row, unsigned col, unsigned step)
{
    static const uint8_t play_icon[8] = { 0u, 0u, 0x20u, 0x38u, 0x3Cu, 0x38u, 0x20u, 0u };
    unsigned border;
    if (row == 0u) border = col;
    else if (col == 7u) border = 7u + row;
    else if (row == 7u) border = 21u - col;
    else if (col == 0u) border = 28u - row;
    else return (play_icon[row] & (0x80u >> col)) ? app_led_piece_color(OT_WHITE, APP_LED_PIECE_LEVEL) : 0u;

    border = (step + APP_LED_WELCOME_STEPS - border) % APP_LED_WELCOME_STEPS;
    return border < 3u ? app_led_piece_color(OT_BLACK, (uint8_t)(60u - 16u * border)) : 0u;
}

#endif
