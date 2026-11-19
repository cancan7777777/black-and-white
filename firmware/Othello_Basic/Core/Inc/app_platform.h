#ifndef APP_PLATFORM_H
#define APP_PLATFORM_H
#include "main.h"
#include "app_basic.h"

typedef struct {
    uint32_t rx_overflows, rx_errors, tx_errors, led_errors, oversized_lines;
} AppPlatformDiagnostics;

void AppPlatform_Init(void);
void AppPlatform_Tick(uint32_t now_ms);
char AppPlatform_TakeKey(void);
uint8_t AppPlatform_ReadFrame(char *out, size_t capacity);
uint8_t AppPlatform_CanSend(void *context);
uint8_t AppPlatform_Send(const char *frame, size_t length, void *context);
void AppPlatform_Render(const AppBasicSnapshot *snapshot, uint8_t cursor, void *context);
void AppPlatform_GetDiagnostics(AppPlatformDiagnostics *out);
/* ISR entry points: only queue data/flags; never run rules or blocking TX. */
void AppPlatform_UartRxIRQ(UART_HandleTypeDef *uart);
void AppPlatform_UartTxIRQ(UART_HandleTypeDef *uart);
void AppPlatform_UartErrorIRQ(UART_HandleTypeDef *uart);
void AppPlatform_PwmDoneIRQ(TIM_HandleTypeDef *timer);
void AppPlatform_PwmErrorIRQ(TIM_HandleTypeDef *timer);
#endif
