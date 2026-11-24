#ifndef APP_LED_DISPLAY_H
#define APP_LED_DISPLAY_H

#include "app_config.h"
#include "othello.h"

#define APP_LED_WELCOME_STEPS 6u
#define APP_LED_WELCOME_STEP_MS 160u

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
    unsigned x = col < 4u ? col : 7u - col;
    unsigned y = row < 4u ? row : 7u - row;
    unsigned reach = step <= 3u ? step : APP_LED_WELCOME_STEPS - step;
    unsigned distance = x + y;

    if (x == 0u || y == 0u) {
        if (distance > reach) return 0u;
        return app_led_piece_color(OT_BLACK, (uint8_t)(60u - 8u * (reach - distance)));
    }
    return distance >= 5u ? app_led_piece_color(OT_WHITE, (uint8_t)(28u + 8u * reach)) : 0u;
}

#endif
