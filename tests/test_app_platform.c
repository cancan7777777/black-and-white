#include "app_platform.h"
#include "ot_protocol.h"
#include <assert.h>
#include <stdio.h>
#include <string.h>
UART_HandleTypeDef huart1 = { USART1 };
TIM_HandleTypeDef htim1 = { TIM1, HAL_TIM_ACTIVE_CHANNEL_1 };
static uint8_t *rx_target;
static const uint8_t *tx_target;
static uint16_t tx_length, gpio_high = 0xffffu, pressed_keys, *dma_target, dma_length;
static uint32_t tick, irq_mask;
static unsigned dma_starts, dma_stops, aborts;
uint32_t __get_PRIMASK(void) { return irq_mask; }
void __disable_irq(void) { irq_mask = 1u; }
void __set_PRIMASK(uint32_t value) { irq_mask = value; }
uint32_t HAL_GetTick(void) { return tick; }
int HAL_UART_Transmit_IT(UART_HandleTypeDef *u, uint8_t *p, uint16_t n)
{ (void)u; tx_target = p; tx_length = n; return HAL_OK; }
int HAL_UART_Receive_IT(UART_HandleTypeDef *u, uint8_t *p, uint16_t n)
{ (void)u; assert(n == 1u); rx_target = p; return HAL_OK; }
int HAL_UART_AbortReceive(UART_HandleTypeDef *u) { (void)u; ++aborts; return HAL_OK; }
void HAL_GPIO_WritePin(void *port, uint16_t pins, GPIO_PinState value)
{ (void)port; if (value) gpio_high |= pins; else gpio_high &= (uint16_t)~pins; }
GPIO_PinState HAL_GPIO_ReadPin(void *port, uint16_t pin)
{
    static const uint16_t row_pins[4] = { 1u, 2u, 1024u, 2048u };
    unsigned row, col;
    (void)port;
    for (col = 0u; col < 4u && pin != (uint16_t)(4096u << col); ++col) {}
    for (row = 0u; row < 4u; ++row)
        if (!(gpio_high & row_pins[row]) && (pressed_keys & (1u << (row * 4u + col))))
            return GPIO_PIN_RESET;
    return GPIO_PIN_SET;
}
int HAL_TIM_PWM_Start_DMA(TIM_HandleTypeDef *t, uint32_t c, uint32_t *p, uint16_t n)
{ (void)t; (void)c; dma_target = (uint16_t *)p; dma_length = n; ++dma_starts; return HAL_OK; }
int HAL_TIM_PWM_Stop_DMA(TIM_HandleTypeDef *t, uint32_t c)
{ (void)t; (void)c; ++dma_stops; return HAL_OK; }
static void advance(unsigned count)
{ while (count--) AppPlatform_Tick(++tick); }
static void feed(const char *s)
{ while (*s) { assert(rx_target); *rx_target = (uint8_t)*s++; AppPlatform_UartRxIRQ(&huart1); } }
static uint32_t dma_hash(void)
{ unsigned i; uint32_t hash = 0u; for (i=0u; i<dma_length; ++i) hash=hash*31u+dma_target[i]; return hash; }
int main(void)
{
    char frame[OT_LINE_MAX], long_line[210];
    AppBasicSnapshot snapshot;
    AppPlatformDiagnostics diagnostics;
    uint32_t before;
    unsigned i;
    AppPlatform_Init(); AppPlatform_Tick(0u);
    feed("@GET,");
    assert(!AppPlatform_ReadFrame(frame, sizeof(frame)));
    feed("1,1234\n");
    assert(AppPlatform_ReadFrame(frame, sizeof(frame)));
    assert(!strcmp(frame, "@GET,1,1234\n"));
    memset(long_line, 'x', sizeof(long_line)-2u);
    long_line[sizeof(long_line)-2u]='\n'; long_line[sizeof(long_line)-1u]=0;
    feed(long_line);
    for (i=0u; i<5u; ++i) assert(!AppPlatform_ReadFrame(frame, sizeof(frame)));
    feed("@GET,2,1234\n");
    assert(AppPlatform_ReadFrame(frame, sizeof(frame)));
    assert(AppPlatform_Send("A", 1u, 0));
    assert(AppPlatform_Send("B", 1u, 0));
    assert(AppPlatform_Send("C", 1u, 0));
    assert(!AppPlatform_CanSend(0) && !AppPlatform_Send("D", 1u, 0));
    assert(tx_length == 1u && tx_target[0] == 'A');
    AppPlatform_UartTxIRQ(&huart1); advance(1u);
    assert(tx_target[0] == 'B' && AppPlatform_Send("D", 1u, 0));
    pressed_keys=(1u<<1); advance(40u);
    assert(AppPlatform_TakeKey() == '2');
    advance(60u); assert(!AppPlatform_TakeKey());
    pressed_keys=0u; advance(35u);
    pressed_keys=3u; advance(40u); assert(!AppPlatform_TakeKey());
    pressed_keys=1u; advance(40u); assert(!AppPlatform_TakeKey());
    pressed_keys=0u; advance(35u);
    pressed_keys=(1u<<5); advance(40u); assert(AppPlatform_TakeKey() == '5');
    memset(&snapshot, 0, sizeof(snapshot));
    snapshot.game.board[0]=OT_BLACK;
    AppPlatform_Render(&snapshot, 0u, 0); advance(1u);
    assert(dma_starts==1u && dma_length==1793u && dma_target[0]==0u);
    before=dma_hash();
    memset(snapshot.game.board, OT_WHITE, sizeof(snapshot.game.board));
    AppPlatform_Render(&snapshot, 0u, 0); advance(1u);
    assert(dma_starts==1u && dma_hash()==before);
    AppPlatform_PwmDoneIRQ(&htim1); advance(1u);
    assert(dma_starts==2u && dma_stops==1u && dma_hash()!=before);
    for (i=dma_length-256u; i<dma_length; ++i) assert(dma_target[i]==0u);
    advance(22u); /* Missing IRQ times out, forces low and retries the latest board. */
    assert(dma_starts>=3u);
    for (i=0u; i<300u; ++i) { *rx_target='x'; AppPlatform_UartRxIRQ(&huart1); }
    advance(1u);
    feed("\n"); assert(!AppPlatform_ReadFrame(frame, sizeof(frame)));
    feed("@GET,3,1234\n"); assert(AppPlatform_ReadFrame(frame, sizeof(frame)));
    AppPlatform_UartErrorIRQ(&huart1); advance(1u);
    assert(aborts>=2u && !irq_mask);
    AppPlatform_GetDiagnostics(&diagnostics);
    assert(diagnostics.oversized_lines && diagnostics.rx_overflows && diagnostics.rx_errors && diagnostics.led_errors);
    puts("PASS: RX framing/overflow recovery, bounded TX, keyboard debounce/chords, immutable DMA buffer/reset, timeout, UART error recovery");
    return 0;
}
