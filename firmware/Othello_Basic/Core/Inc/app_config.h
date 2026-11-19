#ifndef APP_CONFIG_H
#define APP_CONFIG_H
/* Hardware hypotheses; confirm module orientation and row/column wiring on bench. */
#ifndef APP_LED_SERPENTINE
#define APP_LED_SERPENTINE 0
#endif
#define APP_LED_ZERO_TICKS 23u
#define APP_LED_ONE_TICKS 58u
#define APP_LED_RESET_WORDS 256u /* 320 us at 800 kHz; confirm actual LED revision. */
/* Colour intensity 0..255. Keep modest: 64 white LEDs at level 64 already draw
   roughly 1 A from the 5 V rail. The cursor level must differ from the piece
   level, otherwise the cursor is invisible on a stone of the same hue. */
#define APP_LED_PIECE_LEVEL 48u
#define APP_LED_CURSOR_LEVEL 110u
#define APP_KEY_DEBOUNCE_MS 20u
#endif
