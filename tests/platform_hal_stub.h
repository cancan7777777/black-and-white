#ifndef PLATFORM_HAL_STUB_H
#define PLATFORM_HAL_STUB_H
#define __MAIN_H
#define __TIM_H__
#define __USART_H__
#include <stdint.h>
#include <stddef.h>
typedef struct { void *Instance; } UART_HandleTypeDef;
typedef struct { void *Instance; uint32_t Channel; } TIM_HandleTypeDef;
typedef enum { GPIO_PIN_RESET = 0, GPIO_PIN_SET = 1 } GPIO_PinState;
#define HAL_OK 0
#define USART1 ((void *)1)
#define TIM1 ((void *)2)
#define GPIOB ((void *)3)
#define TIM_CHANNEL_1 0u
#define HAL_TIM_ACTIVE_CHANNEL_1 1u
#define KEY_ROW0_Pin 1u
#define KEY_ROW1_Pin 2u
#define KEY_ROW2_Pin 1024u
#define KEY_ROW3_Pin 2048u
#define KEY_COL0_Pin 4096u
#define KEY_COL1_Pin 8192u
#define KEY_COL2_Pin 16384u
#define KEY_COL3_Pin 32768u
extern UART_HandleTypeDef huart1;
extern TIM_HandleTypeDef htim1;
uint32_t __get_PRIMASK(void);
void __disable_irq(void);
void __set_PRIMASK(uint32_t);
uint32_t HAL_GetTick(void);
int HAL_UART_Transmit_IT(UART_HandleTypeDef *, uint8_t *, uint16_t);
int HAL_UART_Receive_IT(UART_HandleTypeDef *, uint8_t *, uint16_t);
int HAL_UART_AbortReceive(UART_HandleTypeDef *);
void HAL_GPIO_WritePin(void *, uint16_t, GPIO_PinState);
GPIO_PinState HAL_GPIO_ReadPin(void *, uint16_t);
int HAL_TIM_PWM_Start_DMA(TIM_HandleTypeDef *, uint32_t, uint32_t *, uint16_t);
int HAL_TIM_PWM_Stop_DMA(TIM_HandleTypeDef *, uint32_t);
#define __HAL_TIM_SET_COMPARE(timer, channel, value) ((void)(timer), (void)(channel), (void)(value))
#define __HAL_TIM_SET_COUNTER(timer, value) ((void)(timer), (void)(value))
#endif
