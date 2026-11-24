#ifndef APP_KEYPAD_H
#define APP_KEYPAD_H

#include <stdint.h>

static inline uint8_t app_keypad_button(unsigned scan_index)
{
    /* Translate the observed directional scan positions to the actual keycaps. */
    static const uint8_t buttons[16] = {
         1u,  2u,  3u,  4u,
         5u,  6u, 12u,  8u,
         9u,  7u, 11u, 15u,
        13u, 14u, 10u, 16u
    };
    return buttons[scan_index];
}

#endif
