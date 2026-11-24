#ifndef APP_LED_HEART_H
#define APP_LED_HEART_H
#include <stdint.h>

#define APP_LED_HEART_PIXELS 40u
#define APP_LED_HEART_STEP_MS 45u
#define APP_LED_HEART_HOLD_MS 700u
#define APP_LED_HEART_DURATION_MS \
    ((APP_LED_HEART_PIXELS - 1u) * APP_LED_HEART_STEP_MS + APP_LED_HEART_HOLD_MS)

static inline unsigned app_led_heart_visible(uint32_t elapsed)
{
    unsigned lit = elapsed / APP_LED_HEART_STEP_MS + 1u;
    return lit < APP_LED_HEART_PIXELS ? lit : APP_LED_HEART_PIXELS;
}

static inline uint8_t app_led_heart_pixel(unsigned row, unsigned col, unsigned lit)
{
    /* Each positive number is the step at which that pixel lights up. */
    static const uint8_t order[8][8] = {
        { 0,  1,  2,  0,  0,  3,  4,  0},
        { 5,  6,  7,  8,  9, 10, 11, 12},
        {13, 14, 15, 16, 17, 18, 19, 20},
        {21, 22, 23, 24, 25, 26, 27, 28},
        { 0, 29, 30, 31, 32, 33, 34,  0},
        { 0,  0, 35, 36, 37, 38,  0,  0},
        { 0,  0,  0, 39, 40,  0,  0,  0},
        { 0,  0,  0,  0,  0,  0,  0,  0}
    };
    uint8_t step = order[row][col];
    return (uint8_t)(step != 0u && step <= lit);
}

#endif
