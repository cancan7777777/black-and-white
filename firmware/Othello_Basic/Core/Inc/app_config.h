#ifndef APP_CONFIG_H
#define APP_CONFIG_H
/* Hardware hypotheses; confirm module orientation and row/column wiring on bench. */
#ifndef APP_LED_SERPENTINE
#define APP_LED_SERPENTINE 1
#endif
#define APP_LED_ZERO_TICKS 23u
#define APP_LED_ONE_TICKS 58u
#define APP_LED_RESET_WORDS 256u /* 320 us at 800 kHz; confirm actual LED revision. */
#define APP_KEY_DEBOUNCE_MS 20u
#endif
