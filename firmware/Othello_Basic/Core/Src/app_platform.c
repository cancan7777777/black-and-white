#include "app_platform.h"
#include "app_config.h"
#include "ot_protocol.h"
#include "tim.h"
#include "usart.h"
#include <string.h>

#define RX_SIZE 256u
#define TX_SLOTS 4u
#define LED_WORDS (1u + 64u * 24u + APP_LED_RESET_WORDS)
static volatile uint16_t rx_head, rx_tail;
static uint8_t rx_bytes[RX_SIZE], rx_byte;
static volatile uint8_t rx_fault, rx_rearm;
static struct { char bytes[OT_LINE_MAX]; uint16_t length; } tx[TX_SLOTS];
static volatile uint8_t tx_head, tx_tail, tx_active;
static char line[OT_LINE_MAX], key_event;
static size_t line_length;
static uint8_t drop_line;
static AppPlatformDiagnostics diagnostics;
static uint8_t scan_row, key_locked;
static uint16_t scan_mask, candidate_mask, stable_mask;
static uint32_t scan_at, candidate_at;
static const uint16_t rows[4] = { KEY_ROW0_Pin, KEY_ROW1_Pin, KEY_ROW2_Pin, KEY_ROW3_Pin };
static const uint16_t columns[4] = { KEY_COL0_Pin, KEY_COL1_Pin, KEY_COL2_Pin, KEY_COL3_Pin };
static const char keymap[16] = { '1','2','3','A','4','5','6','B','7','8','9','C','*','0','#','D' };
static union { uint32_t alignment; uint16_t words[LED_WORDS]; } pwm;
static uint8_t desired_board[64], desired_row, desired_col, desired_cursor, led_dirty;
static volatile uint8_t led_busy, led_done, led_fault;
static uint32_t led_started;

static uint32_t lock_irq(void) { uint32_t p = __get_PRIMASK(); __disable_irq(); return p; }
static void unlock_irq(uint32_t p) { __set_PRIMASK(p); }

static void tx_start(void)
{
    uint32_t irq = lock_irq();
    if (!tx_active && tx_tail != tx_head) {
        tx_active = 1u;
        if (HAL_UART_Transmit_IT(&huart1, (uint8_t *)tx[tx_tail].bytes, tx[tx_tail].length) != HAL_OK) {
            tx_active = 0u;
            ++diagnostics.tx_errors;
        }
    }
    unlock_irq(irq);
}

uint8_t AppPlatform_CanSend(void *context)
{
    (void)context;
    return (uint8_t)(((tx_head + 1u) % TX_SLOTS) != tx_tail);
}

uint8_t AppPlatform_Send(const char *frame, size_t length, void *context)
{
    uint8_t next;
    uint32_t irq;
    (void)context;
    if (!frame || !length || length >= OT_LINE_MAX) return 0u;
    irq = lock_irq();
    next = (uint8_t)((tx_head + 1u) % TX_SLOTS);
    if (next == tx_tail) { unlock_irq(irq); return 0u; }
    memcpy(tx[tx_head].bytes, frame, length);
    tx[tx_head].length = (uint16_t)length;
    tx_head = next;
    unlock_irq(irq);
    tx_start();
    return 1u;
}

void AppPlatform_UartRxIRQ(UART_HandleTypeDef *uart)
{
    uint16_t next;
    if (!uart || uart->Instance != USART1) return;
    next = (uint16_t)((rx_head + 1u) % RX_SIZE);
    if (next == rx_tail) { rx_fault = 1u; ++diagnostics.rx_overflows; }
    else { rx_bytes[rx_head] = rx_byte; rx_head = next; }
    if (HAL_UART_Receive_IT(&huart1, &rx_byte, 1u) != HAL_OK) rx_rearm = 1u;
}

void AppPlatform_UartTxIRQ(UART_HandleTypeDef *uart)
{
    if (!uart || uart->Instance != USART1 || !tx_active) return;
    tx_tail = (uint8_t)((tx_tail + 1u) % TX_SLOTS);
    tx_active = 0u;
}

void AppPlatform_UartErrorIRQ(UART_HandleTypeDef *uart)
{
    if (!uart || uart->Instance != USART1) return;
    ++diagnostics.rx_errors;
    rx_fault = rx_rearm = 1u;
}

uint8_t AppPlatform_ReadFrame(char *out, size_t capacity)
{
    unsigned budget = 64u;
    uint8_t byte;
    if (!out || capacity < OT_LINE_MAX) return 0u;
    while (budget-- && rx_tail != rx_head) {
        byte = rx_bytes[rx_tail];
        rx_tail = (uint16_t)((rx_tail + 1u) % RX_SIZE);
        if (drop_line) {
            if (byte == '\n') { drop_line = 0u; line_length = 0u; }
            continue;
        }
        if (line_length + 1u >= sizeof(line)) {
            ++diagnostics.oversized_lines;
            line_length = 0u;
            drop_line = (uint8_t)(byte != '\n');
            continue;
        }
        line[line_length++] = (char)byte;
        if (byte == '\n') {
            line[line_length] = 0;
            memcpy(out, line, line_length + 1u);
            line_length = 0u;
            return 1u;
        }
    }
    return 0u;
}

static void scan_keyboard(uint32_t now)
{
    unsigned col, index;
    uint16_t mask;
    if ((uint32_t)(now - scan_at) < 1u) return;
    scan_at = now;
    for (col = 0u; col < 4u; ++col)
        if (HAL_GPIO_ReadPin(GPIOB, columns[col]) == GPIO_PIN_RESET)
            scan_mask |= (uint16_t)(1u << (scan_row * 4u + col));
    HAL_GPIO_WritePin(GPIOB, rows[scan_row], GPIO_PIN_SET);
    scan_row = (uint8_t)((scan_row + 1u) % 4u);
    HAL_GPIO_WritePin(GPIOB, rows[scan_row], GPIO_PIN_RESET);
    if (scan_row) return;
    mask = scan_mask; scan_mask = 0u;
    if (mask != candidate_mask) { candidate_mask = mask; candidate_at = now; }
    if ((uint32_t)(now - candidate_at) < APP_KEY_DEBOUNCE_MS || mask == stable_mask) return;
    stable_mask = mask;
    if (!mask) { key_locked = 0u; return; }
    if (key_locked) return;
    key_locked = 1u; /* A single press or a chord requires a complete release next. */
    if (mask & (uint16_t)(mask - 1u)) return;
    for (index = 0u; index < 16u; ++index)
        if (mask == (uint16_t)(1u << index)) { key_event = keymap[index]; break; }
}

char AppPlatform_TakeKey(void)
{
    char key = key_event;
    key_event = 0;
    return key;
}

void AppPlatform_Render(const AppBasicSnapshot *snapshot, uint8_t cursor, void *context)
{
    (void)context;
    memcpy(desired_board, snapshot->game.board, sizeof(desired_board));
    desired_row = snapshot->cursor_row;
    desired_col = snapshot->cursor_col;
    desired_cursor = cursor;
    led_dirty = 1u; /* Coalesce changes; never modify an active DMA buffer. */
}

void AppPlatform_PwmDoneIRQ(TIM_HandleTypeDef *timer)
{
    if (timer && timer->Instance == TIM1 && timer->Channel == HAL_TIM_ACTIVE_CHANNEL_1)
        led_done = 1u;
}

void AppPlatform_PwmErrorIRQ(TIM_HandleTypeDef *timer)
{
    if (timer && timer->Instance == TIM1) led_fault = led_done = 1u;
}

static void refresh_led(uint32_t now)
{
    unsigned pixel, row, col, index, bit, word;
    uint32_t color;
    uint8_t red, green, blue, piece;
    if (led_busy && (led_done || (uint32_t)(now - led_started) > 20u)) {
        if (!led_done || led_fault) { ++diagnostics.led_errors; led_dirty = 1u; }
        (void)HAL_TIM_PWM_Stop_DMA(&htim1, TIM_CHANNEL_1);
        __HAL_TIM_SET_COMPARE(&htim1, TIM_CHANNEL_1, 0u);
        led_busy = led_done = led_fault = 0u;
    }
    if (led_busy || !led_dirty) return;
    word = 0u;
    pwm.words[word++] = 0u;
    for (pixel = 0u; pixel < 64u; ++pixel) {
        row = pixel / 8u; col = pixel % 8u;
        if (APP_LED_SERPENTINE && (row & 1u)) col = 7u - col;
        index = row * 8u + col;
        piece = desired_board[index];
        red = green = blue = 0u;
        if (piece == OT_BLACK) { green = 1u; blue = 6u; }
        if (piece == OT_WHITE) red = green = blue = 6u;
        if (desired_cursor && row == desired_row && col == desired_col) {
            red = 6u; green = 3u; blue = 0u;
        }
        color = ((uint32_t)green << 16) | ((uint32_t)red << 8) | blue;
        for (bit = 0u; bit < 24u; ++bit)
            pwm.words[word++] = (color & (1UL << (23u - bit))) ? APP_LED_ONE_TICKS : APP_LED_ZERO_TICKS;
    }
    while (word < LED_WORDS) pwm.words[word++] = 0u;
    led_done = led_fault = 0u;
    __HAL_TIM_SET_COMPARE(&htim1, TIM_CHANNEL_1, 0u);
    __HAL_TIM_SET_COUNTER(&htim1, 0u);
    led_busy = 1u;
    led_started = now;
    if (HAL_TIM_PWM_Start_DMA(&htim1, TIM_CHANNEL_1, (uint32_t *)pwm.words, (uint16_t)LED_WORDS) != HAL_OK) {
        led_busy = 0u;
        ++diagnostics.led_errors;
    } else led_dirty = 0u;
}

void AppPlatform_Init(void)
{
    rx_head = rx_tail = 0u;
    tx_head = tx_tail = tx_active = 0u;
    rx_fault = 0u; rx_rearm = 1u;
    line_length = 0u; drop_line = 0u; key_event = 0;
    scan_row = key_locked = 0u;
    scan_mask = candidate_mask = stable_mask = 0u;
    scan_at = candidate_at = HAL_GetTick();
    memset(&diagnostics, 0, sizeof(diagnostics));
    HAL_GPIO_WritePin(GPIOB, rows[0] | rows[1] | rows[2] | rows[3], GPIO_PIN_SET);
    HAL_GPIO_WritePin(GPIOB, rows[0], GPIO_PIN_RESET);
    led_busy = led_done = led_fault = led_dirty = 0u;
}

void AppPlatform_Tick(uint32_t now)
{
    uint32_t irq;
    if (rx_fault) {
        irq = lock_irq();
        rx_tail = rx_head; rx_fault = 0u;
        unlock_irq(irq);
        line_length = 0u; drop_line = 1u;
    }
    if (rx_rearm) {
        (void)HAL_UART_AbortReceive(&huart1);
        rx_rearm = (uint8_t)(HAL_UART_Receive_IT(&huart1, &rx_byte, 1u) != HAL_OK);
    }
    scan_keyboard(now);
    refresh_led(now);
    tx_start();
}

void AppPlatform_GetDiagnostics(AppPlatformDiagnostics *out)
{
    uint32_t irq;
    if (!out) return;
    irq = lock_irq(); *out = diagnostics; unlock_irq(irq);
}
