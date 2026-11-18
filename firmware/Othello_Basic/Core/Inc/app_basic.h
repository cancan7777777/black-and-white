#ifndef APP_BASIC_H
#define APP_BASIC_H
#include "othello.h"
#include <stddef.h>

#define APP_BASIC_API_VERSION 0x00010000UL
#define APP_BASIC_MAX_OBSERVERS 4u

typedef struct {
    uint32_t api_version;
    OtGame game; /* Value copy: never a writable pointer to the authoritative game. */
    uint8_t cursor_row, cursor_col;
} AppBasicSnapshot;

typedef enum { APP_SOURCE_BOOT, APP_SOURCE_KEYPAD, APP_SOURCE_SERIAL,
               APP_SOURCE_EXTENSION, APP_SOURCE_RESET } AppBasicSource;
typedef enum { APP_MOVE_OK, APP_MOVE_STALE, APP_MOVE_ILLEGAL,
               APP_MOVE_REENTRANT } AppBasicMoveResult;
typedef void (*AppBasicObserver)(const AppBasicSnapshot *, AppBasicSource, void *);
typedef struct {
    uint8_t (*can_send)(void *);
    uint8_t (*send)(const char *, size_t, void *);
    void (*render)(const AppBasicSnapshot *, uint8_t cursor_visible, void *);
    void *context;
} AppBasicPorts;

void AppBasic_Init(const AppBasicPorts *ports);
void AppBasic_Tick(uint32_t now_ms);
void AppBasic_HandleKey(char key);
/* Returns 0 under TX backpressure: caller must retain and retry this frame. */
uint8_t AppBasic_ProcessFrame(const char *frame);
void AppBasic_GetSnapshot(AppBasicSnapshot *destination);
AppBasicMoveResult AppBasic_RequestMove(uint8_t row, uint8_t col,
                                      uint32_t expected_revision, AppBasicSource source);
int AppBasic_AddObserver(AppBasicObserver observer, void *context);
uint8_t AppBasic_RemoveObserver(unsigned slot);
#endif
